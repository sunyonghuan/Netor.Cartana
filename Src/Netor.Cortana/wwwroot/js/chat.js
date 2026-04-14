/**
 * chat.js - 主入口文件
 * 初始化 Markdown 渲染器、库和协调所有模块
 */

// ============================================================
// 初始化 Markdown 渲染器和代码高亮
// ============================================================
const renderer = new marked.Renderer();
renderer.code = function ({ text, lang }) {
    const language = lang && hljs.getLanguage(lang) ? lang : '';
    const className = language ? ` class="language-${language}"` : '';
    const escaped = text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    return `<pre><code${className}>${escaped}</code></pre>`;
};

marked.setOptions({
    breaks: true,
    gfm: true,
    renderer: renderer
});

// ============================================================
// 初始化应用（由 window.onload 或 document.readyState 触发）
// ============================================================
function initializeApp() {
    // 1. 初始化 DOM 引用（必须首先初始化，所有模块都依赖）
    initializeDOMReferences();

    // 2. 初始化各功能模块
    initializeAttachments();      // 附件管理
    initializeCommandPanel();      // 命令面板
    initializeSelectors();         // 智能体/厂商/模型选择器
    initializeInputHandler();      // 输入框事件处理
    initializeHistory();           // 会话历史
    initializeNewSessionButton();  // 新建会话
    initializeClearButton();       // 清空消息
    initializeSettingsButton();    // 设置按钮

    // 3. 加载初始数据（同步调用，C# 方法是同步的）
    loadSessions();    // 加载会话历史
    loadAgents();      // 加载智能体列表
    loadProviders();   // 加载厂商和模型

    // 4. 启动 WebSocket 连接（最后启动，因为依赖前面的初始化）
    connectWebSocket();
}

// ============================================================
// 初始化时机
// ============================================================
if (document.readyState === 'loading') {
    // DOM 还在加载中
    document.addEventListener('DOMContentLoaded', initializeApp);
} else {
    // DOM 已加载完毕
    initializeApp();
}
