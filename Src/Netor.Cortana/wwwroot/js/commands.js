/**
 * commands.js - 命令面板处理
 */

/**
 * 请求命令列表
 */
function requestCommands(keyword) {
    // 调用 C# 获取命令列表（预留接口）
    if (typeof cortana !== 'undefined' && cortana.getCommands) {
        cortana.getCommands(keyword);
    } else {
        // 演示数据
        onCommandsReceived([
            { name: '搜索文件', desc: '在工作区中搜索文件' },
            { name: '代码片段', desc: '插入常用代码模板' },
            { name: '系统指令', desc: '自定义AI系统提示词' }
        ]);
    }
}

/**
 * 接收命令列表
 */
function onCommandsReceived(items) {
    state.commandItems = items || [];
    state.commandSelectedIndex = 0;
    if (items.length > 0) {
        state.commandVisible = true;
        renderCommandPanel();
    } else {
        hideCommandPanel();
    }
}

/**
 * 渲染命令面板
 */
function renderCommandPanel() {
    dom.commandPanel.innerHTML = state.commandItems.map((item, i) =>
        `<div class="command-item ${i === state.commandSelectedIndex ? 'selected' : ''}" data-index="${i}">` +
        `<span class="cmd-name">#${item.name}</span>` +
        `<span class="cmd-desc">${item.desc}</span>` +
        `</div>`
    ).join('');
    dom.commandPanel.classList.add('visible');

    dom.commandPanel.querySelectorAll('.command-item').forEach(el => {
        el.addEventListener('click', () => selectCommand(parseInt(el.dataset.index)));
    });
}

/**
 * 选择命令
 */
function selectCommand(index) {
    const item = state.commandItems[index];
    if (!item) return;

    const val = dom.textarea.value;
    const before = val.substring(0, state.hashTriggerPos);
    const after = val.substring(dom.textarea.selectionStart);
    dom.textarea.value = before + '#' + item.name + ' ' + after;
    dom.textarea.focus();
    hideCommandPanel();
}

/**
 * 隐藏命令面板
 */
function hideCommandPanel() {
    state.commandVisible = false;
    dom.commandPanel.classList.remove('visible');
}

/**
 * 初始化命令面板逻辑
 */
function initializeCommandPanel() {
    dom.textarea.addEventListener('input', function () {
        const val = this.value;
        const pos = this.selectionStart;

        // 检测 # 触发
        const textBefore = val.substring(0, pos);
        const hashIdx = textBefore.lastIndexOf('#');

        if (hashIdx >= 0) {
            const charBefore = hashIdx > 0 ? textBefore[hashIdx - 1] : ' ';
            if (charBefore === ' ' || charBefore === '\n' || hashIdx === 0) {
                const keyword = textBefore.substring(hashIdx + 1);
                if (!keyword.includes(' ') && !keyword.includes('\n')) {
                    state.hashTriggerPos = hashIdx;
                    requestCommands(keyword);
                    return;
                }
            }
        }

        hideCommandPanel();
    });
}
