//! Lapin desktop shell.
//!
//! The window shows the TypeScript interface; all modelling happens in the C#
//! Lapin engine (`lapin-host`), a local process this shell starts next to itself
//! and stops when the window closes. The interface asks for the engine's
//! address with `engine_url` and talks to it over a local WebSocket.
//!
//! A product package ships the engine with only its product's modules
//! (`resources/product.json`, `resources/modules/<id>/`).
//!
//! In development `pnpm dev:desktop` runs the engine itself and passes its
//! address in `LAPIN_ENGINE_URL`; then nothing is started here.
//!
//! The engine is watched for as long as the app runs: when it stops on its own
//! it is started again (a few times, waiting longer each time), the new port
//! is what `engine_url` answers from then on, and the window hears of it
//! (`engine-restarted` with the new address, or `engine-failed` with why).
//!
//! Updates (the updater plugin) are checked and installed by the interface; it
//! stops the engine with `stop_engine` once the update is downloaded, so the
//! installer never meets a running engine, then restarts the app.

use std::io::{BufRead, BufReader};
use std::path::PathBuf;
use std::process::{Child, Command, Stdio};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Condvar, Mutex};
use std::time::{Duration, Instant};

use tauri::{AppHandle, Emitter, Manager, RunEvent, State, WindowEvent};

/// How many times in a row a stopped engine is started again before giving up.
const RESTARTS: u32 = 3;
/// An engine that ran at least this long before stopping starts the count afresh.
const STABLE_RUN: Duration = Duration::from_secs(60);

#[derive(Default)]
struct Engine {
    url: Mutex<Option<Result<String, String>>>,
    ready: Condvar,
    child: Mutex<Option<Child>>,
    /// Set when the app exits: the engine stopping then is expected.
    stopping: AtomicBool,
}

impl Engine {
    fn settle(&self, r: Result<String, String>) {
        *self.url.lock().unwrap() = Some(r);
        self.ready.notify_all();
    }

    /// The engine is gone: `engine_url` waits for the next one.
    fn unsettle(&self) {
        *self.url.lock().unwrap() = None;
    }

    /// Stop the engine for good (the app is exiting).
    fn stop(&self) {
        self.stopping.store(true, Ordering::SeqCst);
        if let Some(mut child) = self.child.lock().unwrap().take() {
            let _ = child.kill();
            let _ = child.wait();
        }
    }

    /// Wait for the running engine to exit, without holding the lock while it
    /// runs, so that `stop` can always kill it.
    fn wait_exit(&self) -> Option<std::process::ExitStatus> {
        loop {
            {
                let mut guard = self.child.lock().unwrap();
                let child = guard.as_mut()?; // taken by `stop`
                match child.try_wait() {
                    Ok(Some(status)) => {
                        guard.take();
                        return Some(status);
                    }
                    Ok(None) => {}
                    Err(_) => {
                        guard.take();
                        return None;
                    }
                }
            }
            std::thread::sleep(Duration::from_millis(100));
        }
    }
}

/// The engine's WebSocket address, once it has said it is listening.
#[tauri::command]
async fn engine_url(engine: State<'_, Arc<Engine>>) -> Result<String, String> {
    let engine = engine.inner().clone();
    tauri::async_runtime::spawn_blocking(move || {
        let deadline = Instant::now() + Duration::from_secs(60);
        let mut url = engine.url.lock().unwrap();
        while url.is_none() {
            let left = deadline.saturating_duration_since(Instant::now());
            if left.is_zero() {
                return Err("The engine did not start within 60 s.".to_string());
            }
            url = engine.ready.wait_timeout(url, left).unwrap().0;
        }
        url.clone().unwrap()
    })
    .await
    .map_err(|e| e.to_string())?
}

/// Another instance of the app, for a linked file opened on its own (Manage → Links): the new
/// instance reads LAPIN_OPEN at start and imports that file into a new project, editable there.
#[tauri::command]
fn open_separately(path: String) -> Result<(), String> {
    let exe = std::env::current_exe().map_err(|e| e.to_string())?;
    Command::new(exe).env("LAPIN_OPEN", &path).spawn().map_err(|e| e.to_string())?;
    Ok(())
}

/// The file this instance was started for (LAPIN_OPEN), if any.
#[tauri::command]
fn startup_file() -> Option<String> {
    std::env::var("LAPIN_OPEN").ok().filter(|p| !p.is_empty())
}

/// Stop the engine for good, before an update is installed (the app restarts after it).
#[tauri::command]
fn stop_engine(engine: State<'_, Arc<Engine>>) {
    engine.stop();
}

// ─── Ollama, for the model assistant (docs/plan-assistant.md) ───────────────
//
// The interface talks to Ollama with fetch where Ollama's default origins let
// it (tauri://localhost on macOS and Linux). Windows' webview is
// http://tauri.localhost, which Ollama refuses unless OLLAMA_ORIGINS says
// otherwise, so there the HTTP goes through here: only to this computer
// (127.0.0.1), only Ollama's /api/, each line of the answer sent as it comes.

/// The requests being read, by the interface's id: a cancelled one stops at its next line.
#[derive(Default)]
struct OllamaCalls(Mutex<std::collections::HashSet<u32>>);

/// Read an HTTP body (chunked or not) line by line; `each` returns false to stop.
fn read_lines<R: BufRead>(mut r: R, chunked: bool, mut each: impl FnMut(String) -> bool) -> std::io::Result<()> {
    let mut pending: Vec<u8> = Vec::new();
    let mut flush = |pending: &mut Vec<u8>, all: bool, each: &mut dyn FnMut(String) -> bool| -> bool {
        while let Some(i) = pending.iter().position(|&b| b == b'\n') {
            let line: Vec<u8> = pending.drain(..=i).collect();
            let text = String::from_utf8_lossy(&line).trim().to_string();
            if !text.is_empty() && !each(text) {
                return false;
            }
        }
        if all && !pending.is_empty() {
            let text = String::from_utf8_lossy(pending).trim().to_string();
            pending.clear();
            if !text.is_empty() && !each(text) {
                return false;
            }
        }
        true
    };
    if !chunked {
        let mut buf = [0u8; 8192];
        loop {
            let n = std::io::Read::read(&mut r, &mut buf)?;
            if n == 0 {
                break;
            }
            pending.extend_from_slice(&buf[..n]);
            if !flush(&mut pending, false, &mut each) {
                return Ok(());
            }
        }
        flush(&mut pending, true, &mut each);
        return Ok(());
    }
    loop {
        let mut size = String::new();
        if r.read_line(&mut size)? == 0 {
            break;
        }
        let size = usize::from_str_radix(size.trim().split(';').next().unwrap_or("0"), 16)
            .map_err(|e| std::io::Error::new(std::io::ErrorKind::InvalidData, e))?;
        if size == 0 {
            break;
        }
        let mut chunk = vec![0u8; size];
        std::io::Read::read_exact(&mut r, &mut chunk)?;
        let mut crlf = String::new();
        r.read_line(&mut crlf)?;
        pending.extend_from_slice(&chunk);
        if !flush(&mut pending, false, &mut each) {
            return Ok(());
        }
    }
    flush(&mut pending, true, &mut each);
    Ok(())
}

/// One request to Ollama on this computer; the answer's lines go to `on_line`, then a NUL "end" line. Ok(the HTTP status).
#[tauri::command]
async fn ollama_request(
    port: u16,
    method: String,
    path: String,
    body: Option<String>,
    id: u32,
    on_line: tauri::ipc::Channel<String>,
    calls: State<'_, Arc<OllamaCalls>>,
) -> Result<u16, String> {
    use std::io::Write;
    if !path.starts_with("/api/") || path.contains("..") || path.contains(char::is_whitespace) {
        return Err("Only Ollama's /api/ is reached.".to_string());
    }
    let method = if method == "GET" { "GET" } else { "POST" };
    let calls = calls.inner().clone();
    tauri::async_runtime::spawn_blocking(move || {
        let mut stream = std::net::TcpStream::connect(("127.0.0.1", port)).map_err(|e| e.to_string())?;
        let body = body.unwrap_or_default();
        let head = format!(
            "{method} {path} HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n",
            body.len()
        );
        stream.write_all(head.as_bytes()).and_then(|_| stream.write_all(body.as_bytes())).map_err(|e| e.to_string())?;
        let mut reader = BufReader::new(stream);
        let mut status_line = String::new();
        reader.read_line(&mut status_line).map_err(|e| e.to_string())?;
        let status: u16 = status_line.split_whitespace().nth(1).and_then(|s| s.parse().ok()).ok_or("Ollama answered something else.")?;
        let mut chunked = false;
        loop {
            let mut h = String::new();
            if reader.read_line(&mut h).map_err(|e| e.to_string())? == 0 || h.trim().is_empty() {
                break;
            }
            let lower = h.to_ascii_lowercase();
            if lower.starts_with("transfer-encoding:") && lower.contains("chunked") {
                chunked = true;
            }
        }
        let result = read_lines(reader, chunked, |line| {
            if calls.0.lock().unwrap().remove(&id) {
                return false;
            }
            on_line.send(line).is_ok()
        });
        calls.0.lock().unwrap().remove(&id);
        let _ = on_line.send("\u{0}end".to_string());
        result.map(|_| status).map_err(|e| e.to_string())
    })
    .await
    .map_err(|e| e.to_string())?
}

/// Stop reading a request (the Stop button).
#[tauri::command]
fn ollama_cancel(id: u32, calls: State<'_, Arc<OllamaCalls>>) {
    calls.0.lock().unwrap().insert(id);
}

/// The port from the engine's ready line: `LAPIN_HOST_READY {"port":12345}`.
fn ready_port(line: &str) -> Option<u16> {
    let json = line.strip_prefix("LAPIN_HOST_READY ")?;
    let v: serde_json::Value = serde_json::from_str(json.trim()).ok()?;
    v.get("port")?.as_u64().and_then(|p| u16::try_from(p).ok())
}

fn host_binary() -> Result<PathBuf, String> {
    let exe = std::env::current_exe().map_err(|e| e.to_string())?;
    let dir = exe.parent().ok_or("no executable directory")?;
    let name = if cfg!(windows) { "lapin-host.exe" } else { "lapin-host" };
    Ok(dir.join(name))
}

fn spawn_engine(app: &AppHandle) -> Result<Child, String> {
    let host = host_binary()?;
    let res = app.path().resource_dir().map_err(|e| e.to_string())?.join("resources");
    // The user's own data (the recovery journals) lives in the app's data folder.
    let user = app.path().app_data_dir().map_err(|e| e.to_string())?;
    let mut cmd = Command::new(&host);
    cmd.arg("--product").arg(res.join("product.json"))
        .arg("--modules-dir").arg(res.join("modules"))
        .arg("--user-dir").arg(user)
        .args(["--port", "0", "--parent-pid", &std::process::id().to_string()])
        .stdout(Stdio::piped())
        .stderr(Stdio::inherit());
    // On Windows the engine is a console program: started without a console window of its own.
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        const CREATE_NO_WINDOW: u32 = 0x0800_0000;
        cmd.creation_flags(CREATE_NO_WINDOW);
    }
    cmd.spawn()
        .map_err(|e| format!("Cannot start the engine ({}): {e}", host.display()))
}

/// One run of the engine: start it, settle its address from the ready line,
/// read its output to the end, and wait for it to exit. Ok(how long it ran)
/// when it stopped after saying it was ready; Err when it never got there.
fn run_engine(app: &AppHandle, engine: &Engine, restart: bool) -> Result<Duration, String> {
    let mut child = spawn_engine(app)?;
    let stdout = child.stdout.take().ok_or("The engine has no output.")?;
    {
        let mut slot = engine.child.lock().unwrap();
        if engine.stopping.load(Ordering::SeqCst) {
            let _ = child.kill();
            let _ = child.wait();
            return Ok(Duration::ZERO);
        }
        *slot = Some(child);
    }
    let started = Instant::now();
    let mut settled = false;
    for line in BufReader::new(stdout).lines().map_while(Result::ok) {
        if !settled {
            if let Some(port) = ready_port(&line) {
                let url = format!("ws://127.0.0.1:{port}/ws");
                engine.settle(Ok(url.clone()));
                if restart {
                    let _ = app.emit("engine-restarted", url);
                }
                settled = true;
            }
        }
    }
    let status = engine.wait_exit();
    if engine.stopping.load(Ordering::SeqCst) {
        return Ok(started.elapsed());
    }
    let how = status.map(|s| s.to_string()).unwrap_or_else(|| "unknown status".to_string());
    eprintln!("lapin: the engine stopped ({how})");
    engine.unsettle();
    if settled { Ok(started.elapsed()) } else { Err(format!("The engine stopped before it started ({how}).")) }
}

fn start_engine(app: &AppHandle, engine: Arc<Engine>) {
    if let Ok(url) = std::env::var("LAPIN_ENGINE_URL") {
        engine.settle(Ok(url));
        return;
    }
    let app = app.clone();
    std::thread::spawn(move || {
        let mut failures = 0u32;
        let mut restart = false;
        loop {
            let outcome = run_engine(&app, &engine, restart);
            if engine.stopping.load(Ordering::SeqCst) {
                return;
            }
            let error = match outcome {
                Ok(ran) => {
                    if ran >= STABLE_RUN {
                        failures = 0;
                    }
                    "The engine stopped.".to_string()
                }
                Err(e) => e,
            };
            failures += 1;
            if failures > RESTARTS {
                let msg = format!("{error} It could not be restarted: quit and reopen Lapin.");
                engine.settle(Err(msg.clone()));
                let _ = app.emit("engine-failed", msg);
                return;
            }
            // 1 s, 2 s, 4 s…
            std::thread::sleep(Duration::from_secs(1 << (failures - 1)));
            restart = true;
        }
    });
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let engine = Arc::new(Engine::default());
    let app = tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .plugin(tauri_plugin_process::init())
        .manage(engine.clone())
        .manage(Arc::new(OllamaCalls::default()))
        .invoke_handler(tauri::generate_handler![engine_url, open_separately, startup_file, stop_engine, ollama_request, ollama_cancel])
        .setup({
            let engine = engine.clone();
            move |app| {
                start_engine(app.handle(), engine);
                Ok(())
            }
        })
        .build(tauri::generate_context!())
        .expect("error while building Lapin");

    app.run(move |app, event| match event {
        RunEvent::Exit => engine.stop(),
        // The main window gone: its detached view windows go with it.
        RunEvent::WindowEvent { label, event: WindowEvent::Destroyed, .. } if label == "main" => {
            for (_, w) in app.webview_windows() {
                let _ = w.close();
            }
        }
        _ => {}
    });
}

#[cfg(test)]
mod tests {
    use super::{read_lines, ready_port};

    #[test]
    fn reads_an_answer_line_by_line_chunked_or_not() {
        let mut lines = Vec::new();
        // Two chunks that split a line in the middle, then the last chunk.
        let body = b"6\r\n{\"a\":1\r\n7\r\n}\n{\"b\":\r\n3\r\n2}\n\r\n0\r\n\r\n";
        read_lines(&body[..], true, |l| { lines.push(l); true }).unwrap();
        assert_eq!(lines, vec!["{\"a\":1}", "{\"b\":2}"]);
        let mut plain = Vec::new();
        read_lines(&b"{\"x\":1}\n{\"y\":2}"[..], false, |l| { plain.push(l); true }).unwrap();
        assert_eq!(plain, vec!["{\"x\":1}", "{\"y\":2}"]);
        let mut first = Vec::new();
        read_lines(&b"1\n2\n3\n"[..], false, |l| { first.push(l); false }).unwrap();
        assert_eq!(first, vec!["1"]);
    }

    #[test]
    fn reads_the_port_from_the_ready_line() {
        assert_eq!(ready_port("LAPIN_HOST_READY {\"port\":51234}"), Some(51234));
        assert_eq!(ready_port("LAPIN_HOST_READY {\"port\":0}"), Some(0));
        assert_eq!(ready_port("something else"), None);
        assert_eq!(ready_port("LAPIN_HOST_READY nonsense"), None);
    }
}
