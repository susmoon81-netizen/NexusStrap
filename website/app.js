(function () {
  "use strict";

  var REPO = "susmoon81-netizen/NexusStrap";

  // Reveal on scroll
  try {
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add("visible");
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.12 });

    document.querySelectorAll(".reveal").forEach(function (el) {
      observer.observe(el);
    });
  } catch (e) {
    document.querySelectorAll(".reveal").forEach(function (el) {
      el.classList.add("visible");
    });
  }

  // Live version + download link from the latest GitHub release
  fetch("https://api.github.com/repos/" + REPO + "/releases/latest")
    .then(function (res) { return res.ok ? res.json() : null; })
    .then(function (rel) {
      if (!rel) return;

      var version = (rel.tag_name || "").replace(/^v/i, "") || rel.name || "";
      if (version) {
        setText("version-pill", "v" + version);
        setText("stat-version", version);
      }

      var assets = rel.assets || [];
      for (var i = 0; i < assets.length; i++) {
        var name = (assets[i].name || "").toLowerCase();
        if (name === "nexusstrap.exe" && assets[i].browser_download_url) {
          document.getElementById("download-exe").href = assets[i].browser_download_url;
          break;
        }
      }
    })
    .catch(function () { /* keep static fallbacks */ });

  // Live online count from the strap presence API
  fetch("/api/presence")
    .then(function (res) { return res.ok ? res.json() : null; })
    .then(function (data) {
      if (data && typeof data.online === "number") {
        setText("stat-online", String(data.online));
      }
    })
    .catch(function () { /* counter stays a dash */ });

  function setText(id, text) {
    var el = document.getElementById(id);
    if (el) el.textContent = text;
  }
})();
