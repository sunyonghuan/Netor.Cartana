/**
 * selectors.js - 选择器逻辑（提供商、模型、智能体）
 */

/**
 * 设置选择器通用逻辑
 */
function setupSelector(btnEl, panelEl, selectorEl) {
    btnEl.addEventListener('click', (e) => {
        e.stopPropagation();
        // 关闭其他面板
        document.querySelectorAll('.selector.open').forEach(s => {
            if (s !== selectorEl) s.classList.remove('open');
        });
        selectorEl.classList.toggle('open');
    });
}

/**
 * 填充选择器数据
 */
function fillSelector(panelEl, btnEl, items, labelKey) {
    // 优先选中默认项，否则选第一个
    const defaultIdx = items.findIndex(it => it.isDefault);
    const activeIdx = defaultIdx >= 0 ? defaultIdx : 0;

    panelEl.innerHTML = items.map((item, i) =>
        `<div class="selector-item ${i === activeIdx ? 'active' : ''}" data-index="${i}">` +
        `<div class="item-name">${item.name}</div>` +
        (item.desc ? `<div class="item-desc">${item.desc}</div>` : '') +
        `</div>`
    ).join('');

    if (items.length > 0) {
        btnEl.querySelector('.label').textContent = items[activeIdx].name;
    }

    panelEl.querySelectorAll('.selector-item').forEach(el => {
        el.addEventListener('click', () => {
            const idx = parseInt(el.dataset.index);
            const item = items[idx];
            panelEl.querySelectorAll('.selector-item').forEach(s => s.classList.remove('active'));
            el.classList.add('active');
            btnEl.querySelector('.label').textContent = item.name;
            btnEl.closest('.selector').classList.remove('open');

            // 回调 C#（传递 ID）
            if (labelKey === 'agent' && typeof cortana !== 'undefined' && cortana.OnAgentChanged) {
                cortana.OnAgentChanged(item.id);
            }
            if (labelKey === 'provider' && typeof cortana !== 'undefined' && cortana.OnProviderChanged) {
                cortana.OnProviderChanged(item.id);
                // 厂商→模型联动：切换厂商后自动加载对应模型
                if (item.id) {
                    currentProviderId = item.id;
                    loadModels(item.id);
                }
            }
            if (labelKey === 'model' && typeof cortana !== 'undefined' && cortana.OnModelChanged) {
                cortana.OnModelChanged(item.id);
            }
        });
    });
}

/**
 * C# 调用：设置智能体列表
 */
function setAgents(items) {
    fillSelector(dom.agentPanel, dom.agentBtn, items, 'agent');
}

/**
 * C# 调用：设置厂商列表
 */
function setProviders(items) {
    fillSelector(dom.providerPanel, dom.providerBtn, items, 'provider');
}

/**
 * C# 调用：设置模型列表（联动厂商切换）
 */
function setModels(items) {
    fillSelector(dom.modelPanel, dom.modelBtn, items, 'model');
}

/**
 * 初始化所有选择器
 */
function initializeSelectors() {
    setupSelector(dom.agentBtn, dom.agentPanel, dom.agentBtn.closest('.selector'));
    setupSelector(dom.providerBtn, dom.providerPanel, dom.providerBtn.closest('.selector'));
    setupSelector(dom.modelBtn, dom.modelPanel, dom.modelBtn.closest('.selector'));

    // 点击空白关闭所有面板
    document.addEventListener('click', () => {
        document.querySelectorAll('.selector.open').forEach(s => s.classList.remove('open'));
        hideCommandPanel();
    });

    // 阻止面板点击冒泡
    document.querySelectorAll('.selector-panel').forEach(p => {
        p.addEventListener('click', e => e.stopPropagation());
    });
}
