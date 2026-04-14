/**
 * dom-refs.js - DOM 引用和初始化
 */

// DOM 元素引用缓存
const dom = {
    messages: null,
    textarea: null,
    sendBtn: null,
    attachBtn: null,
    fileInput: null,
    attachments: null,
    commandPanel: null,
    agentBtn: null,
    agentPanel: null,
    providerBtn: null,
    providerPanel: null,
    modelBtn: null,
    modelPanel: null,
    historySelector: null,
    historyBtn: null,
    historyPanel: null,
    historyLabel: null,
    btnNewSession: null,
    btnClearMessages: null,
    btnSettings: null
};

/**
 * 初始化所有 DOM 引用
 */
function initializeDOMReferences() {
    dom.messages = document.getElementById('chatMessages');
    dom.textarea = document.getElementById('chatInput');
    dom.sendBtn = document.getElementById('btnSend');
    dom.attachBtn = document.getElementById('btnAttach');
    dom.fileInput = document.getElementById('fileInput');
    dom.attachments = document.getElementById('attachments');
    dom.commandPanel = document.getElementById('commandPanel');
    dom.agentBtn = document.getElementById('agentBtn');
    dom.agentPanel = document.getElementById('agentPanel');
    dom.providerBtn = document.getElementById('providerBtn');
    dom.providerPanel = document.getElementById('providerPanel');
    dom.modelBtn = document.getElementById('modelBtn');
    dom.modelPanel = document.getElementById('modelPanel');
    dom.historySelector = document.getElementById('historySelector');
    dom.historyBtn = document.getElementById('historyBtn');
    dom.historyPanel = document.getElementById('historyPanel');
    dom.historyLabel = document.getElementById('historyLabel');
    dom.btnNewSession = document.getElementById('btnNewSession');
    dom.btnClearMessages = document.getElementById('btnClearMessages');
    dom.btnSettings = document.getElementById('btnSettings');
}
