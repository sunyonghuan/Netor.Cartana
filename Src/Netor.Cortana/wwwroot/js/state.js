/**
 * state.js - 全局状态管理
 */

// 应用全局状态
const state = {
    isLoading: false,
    attachments: [],
    commandVisible: false,
    commandItems: [],
    commandSelectedIndex: 0,
    hashTriggerPos: -1,
    currentSessionId: null,
    ws: null,
    streamingEl: null,
    voiceStreaming: false
};

// 当前选中的厂商 ID，用于厂商→模型联动
let currentProviderId = null;

// COM 桥接对象引用
const cortana = chrome.webview.hostObjects.sync.cortana;

/**
 * 时间格式化工具
 */
function formatTimestamp(ts) {
    if (!ts) return '';
    const date = new Date(ts);
    if (isNaN(date.getTime())) return '';
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const yesterday = new Date(today.getTime() - 86400000);
    const target = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const pad = n => String(n).padStart(2, '0');
    if (target.getTime() === today.getTime()) {
        return `今天 ${pad(date.getHours())}:${pad(date.getMinutes())}`;
    }
    if (target.getTime() === yesterday.getTime()) {
        return `昨天 ${pad(date.getHours())}:${pad(date.getMinutes())}`;
    }
    return `${date.getMonth() + 1}月${date.getDate()}日`;
}

/**
 * 设置加载状态
 */
function setLoading(loading) {
    state.isLoading = loading;
    if (dom && dom.sendBtn) {
        dom.sendBtn.classList.toggle('loading', loading);
    }
}

/**
 * 检测滚动条是否在底部（误差范围 50px）
 */
function isScrolledToBottom() {
    if (!dom || !dom.messages) return true;

    const { scrollTop, scrollHeight, clientHeight } = dom.messages;
    // 滚动条距离底部的距离 <= 50px 时认为在底部
    return scrollHeight - scrollTop - clientHeight <= 50;
}

/**
 * 无条件滚动到底部（初始化、新对话时使用）
 */
function forceScrollToBottom() {
    if (dom && dom.messages) {
        dom.messages.scrollTop = dom.messages.scrollHeight;
    }
}

/**
 * 智能滚动到底部（只有在底部时才滚动，否则保留用户位置）
 */
function scrollToBottom() {
    if (isScrolledToBottom()) {
        forceScrollToBottom();
    }
}
