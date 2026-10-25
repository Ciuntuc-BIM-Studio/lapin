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
        .manage(engine.clone())
        .invoke_handler(tauri::generate_handler![engine_url])
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
    use super::ready_port;

    #[test]
    fn reads_the_port_from_the_ready_line() {
        assert_eq!(ready_port("LAPIN_HOST_READY {\"port\":51234}"), Some(51234));
        assert_eq!(ready_port("LAPIN_HOST_READY {\"port\":0}"), Some(0));
        assert_eq!(ready_port("something else"), None);
        assert_eq!(ready_port("LAPIN_HOST_READY nonsense"), None);
    }
}
