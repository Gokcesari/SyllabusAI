// Theme toggle for app pages (excluding login). Persists per browser.
(function () {
  var KEY = "syllabus_theme";
  var body = document.body;
  if (!body || !body.classList.contains("tp")) return;

  function getStoredTheme() {
    var value = localStorage.getItem(KEY);
    return value === "light" ? "light" : "dark";
  }

  function applyTheme(theme) {
    var isLight = theme === "light";
    body.classList.toggle("tl", isLight);
    var toggle = document.getElementById("themeToggleBtn");
    if (toggle) {
      toggle.setAttribute("aria-pressed", isLight ? "true" : "false");
      var label = document.getElementById("themeToggleLabel");
      if (label) label.textContent = isLight ? "Light mode" : "Dark mode";
    }
  }

  function buildToggle() {
    var host = body.querySelector(".sidebar .user-box");
    if (!host || document.getElementById("themeToggle")) return;

    var wrap = document.createElement("div");
    wrap.id = "themeToggle";
    wrap.className = "theme-toggle";
    wrap.innerHTML =
      '<button type="button" id="themeToggleBtn" class="theme-toggle-btn" aria-label="Switch color theme" aria-pressed="false">' +
      '  <span id="themeToggleLabel" class="theme-toggle-label">Dark mode</span>' +
      '  <span class="theme-toggle-switch" aria-hidden="true"></span>' +
      "</button>";

    host.insertBefore(wrap, host.firstChild);
    var btn = wrap.querySelector("#themeToggleBtn");
    btn.addEventListener("click", function () {
      var next = body.classList.contains("tl") ? "dark" : "light";
      localStorage.setItem(KEY, next);
      applyTheme(next);
    });
  }

  buildToggle();
  applyTheme(getStoredTheme());
})();
// Theme toggle for app pages (excluding login). Persists per browser.
(function () {
  var KEY = "syllabus_theme";
  var body = document.body;
  if (!body || !body.classList.contains("tp")) return;

  function getStoredTheme() {
    var value = localStorage.getItem(KEY);
    return value === "light" ? "light" : "dark";
  }

  function applyTheme(theme) {
    var isLight = theme === "light";
    body.classList.toggle("tl", isLight);
    var toggle = document.getElementById("themeToggleBtn");
    if (toggle) {
      toggle.setAttribute("aria-pressed", isLight ? "true" : "false");
      var label = document.getElementById("themeToggleLabel");
      if (label) label.textContent = isLight ? "Light mode" : "Dark mode";
    }
  }

  function buildToggle() {
    var host = body.querySelector(".sidebar .user-box");
    if (!host || document.getElementById("themeToggle")) return;

    var wrap = document.createElement("div");
    wrap.id = "themeToggle";
    wrap.className = "theme-toggle";
    wrap.innerHTML =
      '<button type="button" id="themeToggleBtn" class="theme-toggle-btn" aria-label="Switch color theme" aria-pressed="false">' +
      '  <span id="themeToggleLabel" class="theme-toggle-label">Dark mode</span>' +
      '  <span class="theme-toggle-switch" aria-hidden="true"></span>' +
      "</button>";

    host.insertBefore(wrap, host.firstChild);
    var btn = wrap.querySelector("#themeToggleBtn");
    btn.addEventListener("click", function () {
      var next = body.classList.contains("tl") ? "dark" : "light";
      localStorage.setItem(KEY, next);
      applyTheme(next);
    });
  }

  buildToggle();
  applyTheme(getStoredTheme());
})();
