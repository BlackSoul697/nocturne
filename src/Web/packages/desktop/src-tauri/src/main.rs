// Nocturne desktop companion. Feature #1: CareLink connect.
//
// Medtronic's Auth0 client only allows the com.medtronic.carepartner:/sso custom-scheme
// redirect and login is CAPTCHA-gated, so a plain web flow cannot receive the auth code.
// This app opens the CareLink login in its own webview, lets the user solve the captcha,
// intercepts the custom-scheme redirect via on_navigation, and relays the captured code
// to the Nocturne server, which owns the PKCE exchange and stores the connector secrets.
//
// The app authenticates to Nocturne with a short-lived link code minted by the web UI
// (nocturne-connect://link?server=…&token=…); the token is a tenant-pinned bearer accepted
// only by the CareLink connect endpoints.

#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use serde::{Deserialize, Serialize};
use std::sync::Mutex;
use tauri::{Emitter, Manager, State};
use url::Url;

const LOGIN_WINDOW_LABEL: &str = "carelink-login";
// Match on the scheme prefix, not ":/sso" — the observed redirect uses a single slash.
const REDIRECT_SCHEME_PREFIX: &str = "com.medtronic.carepartner:";

#[derive(Default)]
struct Session {
    server_url: Option<String>,
    token: Option<String>,
    flow_state: Option<String>,
}

type SessionState = Mutex<Session>;

/// Error shape returned to the frontend; `status` carries the upstream HTTP status when the
/// failure came from the Nocturne API (401 ⇒ the link code expired).
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct CommandError {
    status: Option<u16>,
    message: String,
}

impl CommandError {
    fn new(message: impl Into<String>) -> Self {
        Self { status: None, message: message.into() }
    }

    fn http(status: u16, message: impl Into<String>) -> Self {
        Self { status: Some(status), message: message.into() }
    }
}

type CommandResult<T> = Result<T, CommandError>;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct LinkInfo {
    server_url: String,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct StartResponse {
    authorize_url: Option<String>,
    state: Option<String>,
}

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct CompleteResponse {
    success: bool,
    username: Option<String>,
    country: Option<String>,
}

fn http_client() -> CommandResult<reqwest::Client> {
    reqwest::Client::builder()
        // Local dev runs behind the Aspire gateway's self-signed certificate.
        .danger_accept_invalid_certs(cfg!(debug_assertions))
        .build()
        .map_err(|e| CommandError::new(format!("Could not create HTTP client: {e}")))
}

/// Parses and stores a `nocturne-connect://link?server=…&token=…` link code.
#[tauri::command]
fn link(link_code: String, session: State<'_, SessionState>) -> CommandResult<LinkInfo> {
    let parsed = Url::parse(link_code.trim())
        .map_err(|_| CommandError::new("That doesn't look like a link code. Copy the whole nocturne-connect:// line from Nocturne."))?;

    if parsed.scheme() != "nocturne-connect" {
        return Err(CommandError::new(
            "That doesn't look like a link code. Copy the whole nocturne-connect:// line from Nocturne.",
        ));
    }

    let mut server = None;
    let mut token = None;
    for (key, value) in parsed.query_pairs() {
        match key.as_ref() {
            "server" => server = Some(value.to_string()),
            "token" => token = Some(value.to_string()),
            _ => {}
        }
    }

    let (server, token) = match (server, token) {
        (Some(s), Some(t)) if !s.is_empty() && !t.is_empty() => (s, t),
        _ => return Err(CommandError::new("The link code is missing its server or token part. Generate a fresh one in Nocturne.")),
    };

    let server_url = Url::parse(&server)
        .ok()
        .filter(|u| matches!(u.scheme(), "http" | "https"))
        .ok_or_else(|| CommandError::new("The link code's server address is not a valid URL."))?;

    let server_url = server_url.as_str().trim_end_matches('/').to_string();

    let mut session = session.lock().unwrap();
    session.server_url = Some(server_url.clone());
    session.token = Some(token);
    session.flow_state = None;

    Ok(LinkInfo { server_url })
}

/// Starts the connect flow: asks the server for the Auth0 authorize URL (PKCE verifier stays
/// server-side) and opens it in a dedicated login webview that intercepts the redirect.
///
/// Async on purpose: building a window from a sync command deadlocks on Windows.
#[tauri::command]
async fn start_connect(region: String, app: tauri::AppHandle) -> CommandResult<()> {
    let (server_url, token) = {
        let session = app.state::<SessionState>();
        let session = session.lock().unwrap();
        match (&session.server_url, &session.token) {
            (Some(s), Some(t)) => (s.clone(), t.clone()),
            _ => return Err(CommandError::new("Not linked to a Nocturne server yet.")),
        }
    };

    let response = http_client()?
        .post(format!("{server_url}/api/v4/connectors/carelink/connect/start"))
        .bearer_auth(&token)
        .json(&serde_json::json!({ "server": region }))
        .send()
        .await
        .map_err(|e| CommandError::new(format!("Could not reach {server_url}: {e}")))?;

    let status = response.status().as_u16();
    if !response.status().is_success() {
        return Err(CommandError::http(status, format!("The server rejected the request (HTTP {status}).")));
    }

    let body: StartResponse = response
        .json()
        .await
        .map_err(|e| CommandError::new(format!("Unexpected response from the server: {e}")))?;

    let (authorize_url, flow_state) = match (body.authorize_url, body.state) {
        (Some(a), Some(s)) if !a.is_empty() && !s.is_empty() => (a, s),
        _ => return Err(CommandError::new("The server did not return a sign-in URL.")),
    };

    let authorize_url: Url = authorize_url
        .parse()
        .map_err(|_| CommandError::new("The server returned an invalid sign-in URL."))?;

    {
        let session = app.state::<SessionState>();
        session.lock().unwrap().flow_state = Some(flow_state);
    }

    // Reuse an existing login window (e.g. "start over" while one is open).
    if let Some(existing) = app.get_webview_window(LOGIN_WINDOW_LABEL) {
        let _ = existing.close();
    }

    let handle = app.clone();
    tauri::WebviewWindowBuilder::new(
        &app,
        LOGIN_WINDOW_LABEL,
        tauri::WebviewUrl::External(authorize_url),
    )
    .title("Sign in to CareLink")
    .inner_size(520.0, 760.0)
    .on_navigation(move |url| {
        if url.as_str().starts_with(REDIRECT_SCHEME_PREFIX) {
            if let Some(code) = url
                .query_pairs()
                .find(|(k, _)| k == "code")
                .map(|(_, v)| v.to_string())
            {
                let _ = handle.emit("carelink-code", code);
                if let Some(window) = handle.get_webview_window(LOGIN_WINDOW_LABEL) {
                    let _ = window.close();
                }
            }
            return false; // cancel — the webview can't open the custom scheme anyway
        }
        true
    })
    .build()
    .map_err(|e| CommandError::new(format!("Could not open the sign-in window: {e}")))?;

    Ok(())
}

/// Relays the captured code to the server, which exchanges it (PKCE) and stores the
/// connector secrets for the linked tenant.
#[tauri::command]
async fn complete_connect(code: String, app: tauri::AppHandle) -> CommandResult<CompleteResponse> {
    let (server_url, token, flow_state) = {
        let session = app.state::<SessionState>();
        let session = session.lock().unwrap();
        match (&session.server_url, &session.token, &session.flow_state) {
            (Some(s), Some(t), Some(f)) => (s.clone(), t.clone(), f.clone()),
            _ => return Err(CommandError::new("No sign-in in progress.")),
        }
    };

    let response = http_client()?
        .post(format!("{server_url}/api/v4/connectors/carelink/connect/complete"))
        .bearer_auth(&token)
        .json(&serde_json::json!({ "code": code, "state": flow_state }))
        .send()
        .await
        .map_err(|e| CommandError::new(format!("Could not reach {server_url}: {e}")))?;

    let status = response.status().as_u16();
    if !response.status().is_success() {
        return Err(CommandError::http(status, format!("The server rejected the sign-in (HTTP {status}).")));
    }

    let body: CompleteResponse = response
        .json()
        .await
        .map_err(|e| CommandError::new(format!("Unexpected response from the server: {e}")))?;

    {
        let session = app.state::<SessionState>();
        session.lock().unwrap().flow_state = None;
    }

    Ok(body)
}

/// Closes the login webview if it is open (user cancelled).
#[tauri::command]
fn cancel_login(app: tauri::AppHandle) {
    if let Some(window) = app.get_webview_window(LOGIN_WINDOW_LABEL) {
        let _ = window.close();
    }
}

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_updater::Builder::new().build())
        .plugin(tauri_plugin_process::init())
        .manage(SessionState::default())
        .on_window_event(|window, event| {
            // Let the frontend leave its "waiting for sign-in" state if the user closes the
            // login window. Fires after a capture too; the frontend ignores it then.
            if window.label() == LOGIN_WINDOW_LABEL
                && matches!(event, tauri::WindowEvent::Destroyed)
            {
                let _ = window.app_handle().emit("carelink-login-closed", ());
            }
        })
        .invoke_handler(tauri::generate_handler![
            link,
            start_connect,
            complete_connect,
            cancel_login
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
