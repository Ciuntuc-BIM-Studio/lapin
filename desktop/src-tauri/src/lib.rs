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

use std::io::{BufRead, BufReader};
use std::path::PathBuf;
use std::process::{Child, Command, Stdio};
use std::sync::{Arc, Condvar, Mutex};
use std::time::{Duration, Instant};

use tauri::{AppHandle, Manager, RunEvent, State};

#[derive(Default)]
struct Engine {
    url: Mutex<Option<Result<String, String>>>,
    ready: Condvar,
    child: Mutex<Option<Child>>,
}

impl Engine {
    fn settle(&self, r: Result<String, String>) {
        *self.url.lock().unwrap() = Some(r);
        self.ready.notify_all();
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

fn start_engine(app: &AppHandle, engine: Arc<Engine>) {
    if let Ok(url) = std::env::var("LAPIN_ENGINE_URL") {
        engine.settle(Ok(url));
        return;
    }
    let spawn = || -> Result<Child, String> {
        let host = host_binary()?;
        let res = app.path().resource_dir().map_err(|e| e.to_string())?.join("resources");
        // The user's own data (the recovery journals) lives in the app's data folder.
        let user = app.path().app_data_dir().map_err(|e| e.to_string())?;
        Command::new(&host)
            .arg("--product").arg(res.join("product.json"))
            .arg("--modules-dir").arg(res.join("modules"))
            .arg("--user-dir").arg(user)
            .args(["--port", "0", "--parent-pid", &std::process::id().to_string()])
            .stdout(Stdio::piped())
            .stderr(Stdio::inherit())
            .spawn()
            .map_err(|e| format!("Cannot start the engine ({}): {e}", host.display()))
    };
    match spawn() {
        Err(e) => engine.settle(Err(e)),
        Ok(mut child) => {
            let stdout = child.stdout.take().unwrap();
            *engine.child.lock().unwrap() = Some(child);
            let engine = engine.clone();
            std::thread::spawn(move || {
                let mut settled = false;
                for line in BufReader::new(stdout).lines().map_while(Result::ok) {
                    if !settled {
                        if let Some(port) = ready_port(&line) {
                            engine.settle(Ok(format!("ws://127.0.0.1:{port}/ws")));
                            settled = true;
                        }
                    }
                }
                if !settled {
                    engine.settle(Err("The engine stopped before it started.".to_string()));
                }
            });
        }
    }
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

    app.run(move |_, event| {
        if let RunEvent::Exit = event {
            if let Some(mut child) = engine.child.lock().unwrap().take() {
                let _ = child.kill();
                let _ = child.wait();
            }
        }
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
