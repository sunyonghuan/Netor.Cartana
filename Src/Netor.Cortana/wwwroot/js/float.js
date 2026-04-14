/**
 * float.js - 圆形浮动窗口交互逻辑
 */
(function () {
    const container = document.getElementById('floatContainer');
    const floatBridge = chrome.webview.hostObjects.sync.floatBridge;
    let isDragging = false;
    let hasMoved = false;
    let startX = 0;
    let startY = 0;

    // 拖动开始
    container.addEventListener('mousedown', function (e) {
        if (e.button !== 0) return;
        isDragging = true;
        hasMoved = false;
        startX = e.screenX;
        startY = e.screenY;
        document.body.classList.add('dragging');
        e.preventDefault();
    });

    // 拖动中 - 通过 hostWindow 移动窗口
    document.addEventListener('mousemove', function (e) {
        if (!isDragging) return;
        const dx = e.screenX - startX;
        const dy = e.screenY - startY;
        if (Math.abs(dx) > 2 || Math.abs(dy) > 2) {
            hasMoved = true;
            startX = e.screenX;
            startY = e.screenY;
            if (typeof hostWindow !== 'undefined') {
                hostWindow.left = hostWindow.left + dx;
                hostWindow.top = hostWindow.top + dy;
            }
        }
    });

    // 拖动结束
    document.addEventListener('mouseup', function (e) {
        if (!isDragging) return;
        isDragging = false;
        document.body.classList.remove('dragging');

        // 没有移动过 => 当作点击，打开主窗口
        if (!hasMoved) {
            floatBridge.ShowMainWindow();
        }
    });
})();
