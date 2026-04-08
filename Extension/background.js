const SERVER_URL = "http://127.0.0.1:44321/api/download";

// Helper function to extract cookies and send to the server
async function sendToApp(url) {
  try {
    const cookies = await new Promise((resolve) => {
      chrome.cookies.getAll({ url: url }, (res) => resolve(res));
    });

    await fetch(SERVER_URL, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ url: url, cookies: cookies })
    });
  } catch (error) {
    console.error("CBDownloader Sync Error:", error);
  }
}

// Listener for extension icon clicks (action)
chrome.action.onClicked.addListener((tab) => {
  if (tab.url) {
    sendToApp(tab.url);
  }
});

// Automatically trigger background sync when a user opens/switches to a supported tab
// This guarantees the desktop app always has fresh cookies even if the user just copies via Ctrl+C
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.status === 'complete' && tab.url && 
      (tab.url.includes("youtube.com") || tab.url.includes("youtu.be") || tab.url.includes("instagram.com"))) {
    syncCookies(tab.url);
  }
});

async function syncCookies(url) {
  try {
    const cookies = await new Promise((resolve) => {
      chrome.cookies.getAll({ url: url }, (res) => resolve(res));
    });

    if (cookies && cookies.length > 0) {
      await fetch(SERVER_URL + "/sync", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ url: url, cookies: cookies })
      });
    }
  } catch (e) {
    // silently fail
  }
}

// Listener for messages from content scripts
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === "download" && request.url) {
    sendToApp(request.url).then(() => {
      sendResponse({ status: "success" });
    });
    return true; // indicates asynchronous response
  }
});
