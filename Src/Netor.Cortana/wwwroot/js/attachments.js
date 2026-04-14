/**
 * attachments.js - 附件管理
 */

/**
 * 渲染附件列表
 */
function renderAttachments() {
    dom.attachments.innerHTML = state.attachments.map((f, i) =>
        `<div class="attachment-tag">` +
        `<span>📎 ${f.name}</span>` +
        `<span class="remove-attach" data-index="${i}">&times;</span>` +
        `</div>`
    ).join('');

    dom.attachments.querySelectorAll('.remove-attach').forEach(el => {
        el.addEventListener('click', () => {
            state.attachments.splice(parseInt(el.dataset.index), 1);
            renderAttachments();
        });
    });
}

/**
 * 清空附件
 */
function clearAttachments() {
    state.attachments = [];
    dom.attachments.innerHTML = '';
}

/**
 * 初始化附件管理
 */
function initializeAttachments() {
    dom.attachBtn.addEventListener('click', () => {
        if (typeof cortana === 'undefined' || !cortana.OpenFileDialog) return;

        try {
            // 异步调用，结果通过 onFilesSelected 回调返回
            cortana.OpenFileDialog();
        } catch (e) {
            console.error('打开文件选择器失败:', e);
        }
    });

    initializeDragDrop();
}

/**
 * C# 侧文件选择完成后的回调（文件对话框 / 拖放共用）
 */
function onFilesSelected(json) {
    try {
        const files = JSON.parse(json);
        files.forEach(f => {
            state.attachments.push({ path: f.path, name: f.name, type: f.type });
        });
        if (files.length > 0) renderAttachments();
    } catch (e) {
        console.error('处理文件选择结果失败:', e);
    }
}

// ──────── 拖放视觉反馈 ────────

/**
 * 初始化拖放视觉反馈。
 * 文件路径的提取由 C# 端 WM_DROPFILES 处理，前端仅负责高亮提示。
 */
function initializeDragDrop() {
    const dropZone = document.querySelector('.input-area');
    if (!dropZone) return;

    let dragCounter = 0;

    dropZone.addEventListener('dragenter', (e) => {
        e.preventDefault();
        dragCounter++;
        if (dragCounter === 1) {
            dropZone.classList.add('drag-over');
        }
    });

    dropZone.addEventListener('dragover', (e) => {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'copy';
    });

    dropZone.addEventListener('dragleave', (e) => {
        e.preventDefault();
        dragCounter--;
        if (dragCounter <= 0) {
            dragCounter = 0;
            dropZone.classList.remove('drag-over');
        }
    });

    dropZone.addEventListener('drop', (e) => {
        e.preventDefault();
        dragCounter = 0;
        dropZone.classList.remove('drag-over');
        // 文件路径由 C# 端 WM_DROPFILES 自动处理，此处仅清除视觉效果
    });
}
