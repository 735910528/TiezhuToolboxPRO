const state = {
  page: "equipment",
  catalog: null,
  selectedSet: null,
  settingsLock: false,
};

const $ = (id) => document.getElementById(id);

function post(payload) {
  if (window.chrome?.webview?.postMessage)
    window.chrome.webview.postMessage(payload);
}

function setPage(page) {
  state.page = page;
  document.querySelectorAll(".page").forEach((section) => {
    section.classList.toggle("is-active", section.dataset.page === page);
  });
}

function adviceClass(advice) {
  const key = String(advice || "None").toLowerCase();
  if (key === "continue") return "advice-continue";
  if (key === "keep") return "advice-keep";
  if (key === "gamblespeed") return "advice-gamble";
  if (key === "reforge") return "advice-reforge";
  if (key === "giveup" || key === "giveupfixedmain") return "advice-giveup";
  return "advice-none";
}

function renderEquipment(data) {
  if (!data || !data.hasResult) {
    $("eq-score").textContent = "—";
    $("eq-meta").textContent = "点击顶部「截图识别」，或使用识别快捷键";
    $("eq-advice").className = "advice-badge advice-none";
    $("eq-advice").textContent = "等待识别";
    $("eq-advice-detail").textContent = "识别后会在这里给出继续强化、赌速度、重铸或放弃建议。";
    $("eq-main").textContent = "主属性：—";
    $("eq-set").textContent = "套装：—";
    $("eq-subs").innerHTML = "";
    $("eq-recs-title").textContent = "套装需求";
    $("eq-recs").innerHTML = `<div class="empty">识别装备后显示适用子类</div>`;
    return;
  }

  $("eq-score").textContent = data.score;
  $("eq-meta").textContent = data.meta;
  $("eq-advice").className = `advice-badge ${adviceClass(data.advice)}`;
  $("eq-advice").textContent = data.adviceText;
  $("eq-advice-detail").textContent = data.adviceDetail || "";
  $("eq-main").textContent = data.mainStat;
  $("eq-set").textContent = data.setName;
  $("eq-subs").innerHTML = (data.subStats || [])
    .map((item) => `<li><span>${item.name}</span><span>${item.value}</span></li>`)
    .join("");
  $("eq-recs-title").textContent = data.recsTitle || "套装需求";
  const recs = data.recommendations || [];
  if (recs.length === 0) {
    $("eq-recs").innerHTML = `<div class="empty">${data.recsEmpty || "暂无匹配子类"}</div>`;
    return;
  }
  $("eq-recs").innerHTML = recs.map((rec, index) => `
    <article class="rec" data-rec="${index}">
      <div class="rec-head">
        <div>
          <h3>${escapeHtml(rec.profileName)}</h3>
          <p>命中：${escapeHtml((rec.matchedStats || []).join("、") || "无")}　需求权重 ${formatNum(rec.demandWeight)}</p>
          <p>${escapeHtml(rec.mainStatContribution || "左三固定主属性不参与匹配")}</p>
        </div>
        <div class="score-pill">${formatNum(rec.score)}%</div>
        <span>${rec.heroes?.length ? "▼" : ""}</span>
      </div>
      <div class="heroes">
        ${(rec.heroes || []).map((hero) => `
          <div class="hero" title="命中属性：${escapeHtml((hero.matchedStats || []).join("、"))}">
            <img src="${hero.avatar || ""}" alt="" onerror="this.style.visibility='hidden'">
            <div>
              <strong>${escapeHtml(hero.name)}</strong>
              <p>${escapeHtml(hero.comboName)} · 样本 ${formatPercent(hero.sampleShare)} · 需求 ${formatNum(hero.demandContribution)}</p>
            </div>
            <div class="score-pill">${formatNum(hero.score)}%</div>
          </div>
        `).join("")}
      </div>
    </article>
  `).join("");
  $("eq-recs").querySelectorAll(".rec-head").forEach((head) => {
    head.addEventListener("click", () => head.parentElement.classList.toggle("is-open"));
  });
}

function renderDemand() {
  const catalog = state.catalog;
  if (!catalog?.loaded) {
    $("demand-sets").innerHTML = "";
    $("demand-title").textContent = "套装需求分析";
    $("demand-source").textContent = catalog?.error || "需求数据未加载";
    $("demand-profiles").innerHTML = `<div class="empty">${escapeHtml(catalog?.error || "需求数据未加载")}</div>`;
    return;
  }

  $("demand-source").textContent = `内置数据 · 更新于 ${catalog.updatedAt || "未知"}`;
  if (!state.selectedSet)
    state.selectedSet = catalog.sets[0]?.code;
  const selected = catalog.sets.find((set) => set.code === state.selectedSet) || catalog.sets[0];
  $("demand-sets").innerHTML = catalog.sets.map((set) => {
    const enabled = (set.profiles || []).filter((profile) => profile.enabled).length;
    const summary = set.profiles.length === 0
      ? "暂无需求数据"
      : enabled === set.profiles.length
        ? `${set.profiles.length} 个属性子类`
        : `${enabled}/${set.profiles.length} 个参与匹配`;
    return `
      <button type="button" class="set-item ${set.code === selected.code ? "is-active" : ""}" data-code="${set.code}">
        <img src="${set.icon || ""}" alt="" onerror="this.style.visibility='hidden'">
        <span>${escapeHtml(set.name)}<small>${summary}</small></span>
      </button>`;
  }).join("");
  $("demand-sets").querySelectorAll(".set-item").forEach((button) => {
    button.addEventListener("click", () => {
      state.selectedSet = button.dataset.code;
      post({ type: "selectDemandSet", code: state.selectedSet });
      renderDemand();
    });
  });

  $("demand-title").textContent = selected.name;
  if (!selected.profiles.length) {
    $("demand-profiles").innerHTML = `<div class="empty">${escapeHtml(selected.name)}暂无内置需求数据</div>`;
    return;
  }
  $("demand-profiles").innerHTML = selected.profiles.map((profile) => `
    <article class="profile" data-key="${escapeHtml(profile.key)}">
      <div class="profile-head">
        <div>
          <h3>${escapeHtml(profile.name)}</h3>
          <p>需求权重 ${formatNum(profile.demandWeight)} · ${profile.heroes.length} 条英雄配装</p>
          <p>属性权重：${(profile.stats || []).map((stat) => `${stat} ${formatNum(profile.weights?.[stat])}`).join("　")}</p>
        </div>
        <button type="button" class="switch ${profile.enabled ? "is-on" : ""}" data-key="${escapeHtml(profile.key)}" aria-label="参与匹配"></button>
        <span>${profile.heroes.length ? "▼" : ""}</span>
      </div>
      <div class="heroes">
        ${profile.heroes.map((hero) => `
          <div class="hero">
            <img src="${hero.avatar || ""}" alt="" onerror="this.style.visibility='hidden'">
            <div>
              <strong>${escapeHtml(hero.name)}</strong>
              <p>${escapeHtml(hero.comboName)}｜样本 ${formatPercent(hero.sampleShare)}｜需求 ${formatNum(hero.demandContribution)}</p>
            </div>
          </div>
        `).join("")}
      </div>
    </article>
  `).join("");
  $("demand-profiles").querySelectorAll(".profile-head").forEach((head) => {
    head.addEventListener("click", (event) => {
      if (event.target.closest(".switch"))
        return;
      head.parentElement.classList.toggle("is-open");
    });
  });
  $("demand-profiles").querySelectorAll(".switch").forEach((button) => {
    button.addEventListener("click", (event) => {
      event.stopPropagation();
      const enabled = !button.classList.contains("is-on");
      post({ type: "toggleProfile", key: button.dataset.key, enabled });
    });
  });
}

function renderSettings(data) {
  if (!data)
    return;
  state.settingsLock = true;
  $("st-left").value = data.leftThreshold;
  $("st-right").value = data.rightThreshold;
  $("st-88").value = data.level88Threshold;
  const hotkey = $("st-hotkey");
  hotkey.textContent = data.hotKeyListening ? "按下按键…" : (data.recognitionHotKey || "F2");
  hotkey.classList.toggle("is-listening", !!data.hotKeyListening);
  $("st-continuous").checked = !!data.continuousRecognition;
  $("st-interval").value = data.recognitionIntervalSeconds;
  $("st-rules").innerHTML = (data.rules || []).map((line) => `<li>${escapeHtml(line)}</li>`).join("");
  state.settingsLock = false;
}

function emitSettings() {
  if (state.settingsLock)
    return;
  post({
    type: "settings",
    leftThreshold: Number($("st-left").value),
    rightThreshold: Number($("st-right").value),
    level88Threshold: Number($("st-88").value),
    continuousRecognition: $("st-continuous").checked,
    recognitionIntervalSeconds: Number($("st-interval").value),
  });
}

function formatNum(value) {
  const number = Number(value);
  return Number.isFinite(number) ? number.toFixed(number % 1 === 0 ? 0 : 1).replace(/\.0$/, "") : "0";
}

function formatPercent(value) {
  const number = Number(value);
  return Number.isFinite(number) ? `${(number * 100).toFixed(1)}%` : "0.0%";
}

function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

["st-left", "st-right", "st-88", "st-continuous", "st-interval"].forEach((id) => {
  const node = $(id);
  node.addEventListener("change", emitSettings);
  if (node.tagName === "INPUT" && node.type === "number")
    node.addEventListener("input", emitSettings);
});
$("st-hotkey").addEventListener("click", () => post({ type: "bindHotKey" }));
$("st-open-auto").addEventListener("click", () => post({ type: "openAutoSettings" }));
$("st-reset").addEventListener("click", () => post({ type: "resetSettings" }));

window.chrome?.webview?.addEventListener("message", (event) => {
  const message = event.data || {};
  if (message.type === "page")
    setPage(message.page);
  else if (message.type === "equipment")
    renderEquipment(message);
  else if (message.type === "demand") {
    state.catalog = message.catalog;
    if (message.selectedSet)
      state.selectedSet = message.selectedSet;
    renderDemand();
  }
  else if (message.type === "profileEnabled" && state.catalog) {
    state.catalog.sets.forEach((set) => {
      set.profiles.forEach((profile) => {
        if (profile.key === message.key)
          profile.enabled = message.enabled;
      });
    });
    renderDemand();
  }
  else if (message.type === "settings")
    renderSettings(message);
  else if (message.type === "init") {
    if (message.settings)
      renderSettings(message.settings);
    if (message.catalog) {
      state.catalog = message.catalog;
      state.selectedSet = message.selectedSet || message.catalog.sets?.[0]?.code;
      renderDemand();
    }
    renderEquipment(message.equipment);
    if (message.page)
      setPage(message.page);
  }
});

post({ type: "ready" });
