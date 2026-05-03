// ════════════════════════════════════════════════════════════════════
//  CONFIG — injected by Index.cshtml via window.ChatConfig
// ════════════════════════════════════════════════════════════════════
const apiBaseUrl   = window.ChatConfig?.apiBaseUrl   ?? '';
const currentUserId = window.ChatConfig?.currentUserId ?? '';

// ════════════════════════════════════════════════════════════════════
//  STATE
// ════════════════════════════════════════════════════════════════════
let activeConvId   = null;
let activeConvData = null; // { isBlocked, isHidden, blockedByUserId, otherUserId, otherUserName }
let pendingDeleteMsgId = null;
let pendingReportMsgId = null;
let allConversations = [];
let hubConnection  = null;
const joinedConversationIds = new Set();
const lastRenderedSignatureByConversation = new Map();
let conversationsPollTimer = null;
let messagesPollTimer = null;
let isLoadingConversations = false;
let isLoadingMessages = false;
let isSyncingConversationGroups = false;
const CONVERSATIONS_POLL_INTERVAL_MS = 5000;
const MESSAGES_POLL_INTERVAL_MS = 2500;

function notifyHeaderChatUnread() {
    try { window.dispatchEvent(new CustomEvent('nm:chat-unread-refresh')); } catch { /* ignore */ }
}

function hasValidOtherUserId() {
    const uid = activeConvData?.otherUserId;
    if (uid == null || uid === '') return false;
    return String(uid).toLowerCase() !== '00000000-0000-0000-0000-000000000000';
}

function getOtherUserProfileUrl() {
    if (!hasValidOtherUserId()) return '';
    return '/Profile/ViewUser?userId=' + encodeURIComponent(String(activeConvData.otherUserId));
}

function updateChatHeaderActionButtons() {
    const can = hasValidOtherUserId();
    const v = document.getElementById('btnChatViewProfile');
    if (v) v.disabled = !can;
}

function goToOtherUserProfile() {
    const href = getOtherUserProfileUrl();
    if (href) window.location.href = href;
}

async function markConversationReadOnServer(convId) {
    if (!convId) return;
    try {
        const res = await fetch(`/Communication/MarkRead?conversationId=${encodeURIComponent(convId)}`, {
            method: 'POST',
            credentials: 'same-origin'
        });
        if (res.ok) {
            const conv = allConversations.find(c => String(c.id) === String(convId));
            if (conv) {
                conv.unreadCount = 0;
                renderConversations(allConversations);
            }
        }
    } catch { /* ignore */ }
    notifyHeaderChatUnread();
}

// ════════════════════════════════════════════════════════════════════
//  INIT
// ════════════════════════════════════════════════════════════════════
document.addEventListener('DOMContentLoaded', async () => {
    await loadConversations();
    await initSignalR();
    startRealtimeSync();

    const initialId = window.ChatConfig?.initialConversationId ?? '';
    if (initialId) openConversation(initialId);

    document.getElementById('convSearch').addEventListener('input', e => {
        filterConversations(e.target.value.trim().toLowerCase());
    });

    const ta = document.getElementById('msgInput');
    ta.addEventListener('input', () => {
        ta.style.height = 'auto';
        ta.style.height = Math.min(ta.scrollHeight, 120) + 'px';
    });
    ta.addEventListener('keydown', e => {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); sendMessage(); }
    });

    document.getElementById('sendBtn').addEventListener('click', sendMessage);
    document.getElementById('btnBlockConv').addEventListener('click', toggleBlock);
    document.getElementById('btnChatViewProfile')?.addEventListener('click', goToOtherUserProfile);

    document.addEventListener('click', () => {
        document.querySelectorAll('.msg-ctx-menu.open').forEach(m => m.classList.remove('open'));
    });

    document.addEventListener('visibilitychange', () => {
        if (document.hidden) return;
        loadConversations(true);
        if (activeConvId) loadMessages(activeConvId, 1, true);
    });

    window.addEventListener('beforeunload', stopRealtimeSync);
});

// ════════════════════════════════════════════════════════════════════
//  CONVERSATIONS
// ════════════════════════════════════════════════════════════════════
async function loadConversations(silent = false) {
    if (isLoadingConversations) return;
    isLoadingConversations = true;
    try {
        const res = await fetch('/Communication/Conversations');
        const json = await res.json();
        allConversations = json.data ?? [];
        renderConversations(allConversations);
        await syncConversationGroups();
        notifyHeaderChatUnread();
    } catch {
        if (!silent) {
            document.getElementById('convList').innerHTML = '<p style="text-align:center;color:#9ca3af;padding:20px;font-size:14px;">Không thể tải danh sách.</p>';
        }
    } finally {
        isLoadingConversations = false;
    }
}

function renderConversations(list) {
    const el = document.getElementById('convList');
    if (!list.length) {
        el.innerHTML = '<div style="text-align:center;color:#9ca3af;padding:30px 16px;font-size:14px;"><span class="material-icons-round" style="font-size:36px;color:#fdba74;display:block;margin-bottom:10px;">forum</span>Chưa có hội thoại nào.</div>';
        return;
    }
    el.innerHTML = list.map(c => {
        const initial = (c.otherUserName || 'U').charAt(0).toUpperCase();
        const avatarHtml = c.otherUserAvatar
            ? `<img src="${esc(c.otherUserAvatar)}" alt="${esc(c.otherUserName)}">`
            : initial;
        const isActive = c.id === activeConvId;
        const time = c.lastMessageAt ? fmtTime(c.lastMessageAt) : '';
        const badge = c.unreadCount > 0 ? `<span class="conv-badge">${c.unreadCount > 99 ? '99+' : c.unreadCount}</span>` : '';
        return `<div class="conv-item ${isActive ? 'active' : ''}" onclick="openConversation('${c.id}')" data-id="${c.id}" data-name="${esc(c.otherUserName)}" id="conv-${c.id}">
            <div class="conv-avatar">${avatarHtml}</div>
            <div class="conv-info">
                <div class="conv-name">${esc(c.otherUserName || 'Người dùng')}</div>
                <div class="conv-preview">${esc(c.lastMessage || 'Bắt đầu cuộc trò chuyện...')}</div>
            </div>
            <div class="conv-meta">
                <span class="conv-time">${time}</span>
                ${badge}
            </div>
        </div>`;
    }).join('');
}

function filterConversations(q) {
    if (!q) { renderConversations(allConversations); return; }
    renderConversations(allConversations.filter(c => (c.otherUserName || '').toLowerCase().includes(q)));
}

// ════════════════════════════════════════════════════════════════════
//  OPEN CONVERSATION
// ════════════════════════════════════════════════════════════════════
async function openConversation(id) {
    if (activeConvId === id) return;
    activeConvId = id;

    document.querySelectorAll('.conv-item').forEach(el => el.classList.remove('active'));
    const conv = document.getElementById(`conv-${id}`);
    if (conv) conv.classList.add('active');

    document.getElementById('chatEmpty').style.display = 'none';
    const area = document.getElementById('chatArea');
    area.style.display = 'flex';
    area.style.flexDirection = 'column';

    document.getElementById('chatMessages').innerHTML = '<div style="text-align:center;padding:40px;"><div class="spin"></div></div>';
    document.getElementById('headerName').textContent = conv?.dataset?.name ?? '...';
    activeConvData = null;
    updateChatHeaderActionButtons();

    await loadMessages(id);
    resetUnreadCount(id);
}

// ════════════════════════════════════════════════════════════════════
//  MESSAGES
// ════════════════════════════════════════════════════════════════════
async function loadMessages(convId, page = 1, silent = false) {
    if (!convId) return;
    try {
        const res = await fetch(`/Communication/Messages?conversationId=${convId}&page=${page}&pageSize=50`);
        const json = await res.json();
        const data = json.data;
        if (!data) return;

        document.getElementById('headerName').textContent = data.otherUserName || 'Người dùng';
        const avatarEl = document.getElementById('headerAvatar');
        avatarEl.textContent = (data.otherUserName || 'U').charAt(0).toUpperCase();

        activeConvData = {
            isBlocked: data.isBlocked,
            isHidden: data.isHidden,
            blockedByUserId: data.blockedByUserId || null,
            otherUserId: data.otherUserId,
            otherUserName: data.otherUserName
        };
        updateChatHeaderActionButtons();
        updateBlockUI(data.isBlocked);

        const messages = data.messages ?? [];
        const signature = buildMessageSignature(messages);
        const signatureKey = String(convId);
        if (lastRenderedSignatureByConversation.get(signatureKey) !== signature) {
            renderMessages(messages);
            lastRenderedSignatureByConversation.set(signatureKey, signature);
        }

        const hasUnreadIncoming = messages.some(m =>
            String(m.senderId || '') === String(data.otherUserId || '') && !m.readAt && !m.isDeleted
        );
        if (hasUnreadIncoming) await markConversationReadOnServer(convId);
    } catch {
        document.getElementById('chatMessages').innerHTML = '<p style="text-align:center;color:#9ca3af;padding:20px;">Không thể tải tin nhắn.</p>';
    }
}

function renderMessages(messages) {
    const el = document.getElementById('chatMessages');
    if (!messages.length) {
        el.innerHTML = '<div style="text-align:center;color:#9ca3af;padding:40px;font-size:14px;"><span class="material-icons-round" style="font-size:36px;color:#fdba74;display:block;margin-bottom:10px;">chat_bubble_outline</span>Chưa có tin nhắn nào. Hãy bắt đầu cuộc trò chuyện!</div>';
        return;
    }

    const sorted = [...messages].sort((a, b) => new Date(a.createdAt) - new Date(b.createdAt));
    let lastDate = '';

    el.innerHTML = sorted.map(m => {
        const isMine = resolveMessageIsMine(m);
        const dateStr = new Date(m.createdAt).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
        const dateSep = dateStr !== lastDate ? `<div class="msg-group-date"><span>${dateStr}</span></div>` : '';
        lastDate = dateStr;

        const initial = (m.senderName || 'U').charAt(0).toUpperCase();
        const avatarHtml = m.senderAvatar
            ? `<img src="${esc(m.senderAvatar)}" alt="${esc(m.senderName)}">`
            : initial;

        const ctx = isMine
            ? `<div class="msg-ctx-menu" id="ctx-${m.id}"><div class="ctx-item danger" onclick="openDeleteModal('${m.id}')"><span class="material-icons-round" style="font-size:16px;">delete</span>Xóa</div></div>`
            : `<div class="msg-ctx-menu" id="ctx-${m.id}"><div class="ctx-item danger" onclick="openReportModal('${m.id}')"><span class="material-icons-round" style="font-size:16px;">flag</span>Báo cáo</div></div>`;

        return `${dateSep}
        <div class="msg-row ${isMine ? 'mine' : ''}" id="msg-${m.id}">
            ${!isMine ? `<div class="msg-avatar">${avatarHtml}</div>` : ''}
            <div style="display:flex;flex-direction:column;gap:3px;position:relative;">
                <div class="msg-bubble ${m.isDeleted ? 'deleted' : ''}"
                     oncontextmenu="openCtxMenu(event,'${m.id}')"
                     ondblclick="openCtxMenu(event,'${m.id}')">
                    ${Number(m.messageType) === 4 ? renderHiringOfferCard(m, isMine) : esc(m.content)}
                    ${ctx}
                </div>
                <div class="msg-time">${fmtMsgTime(m.createdAt)}${m.readAt ? ' ✓✓' : ''}</div>
            </div>
            ${isMine ? `<div class="msg-avatar">${avatarHtml}</div>` : ''}
        </div>`;
    }).join('');

    el.scrollTop = el.scrollHeight;
}

function appendMessage(m) {
    const el = document.getElementById('chatMessages');
    const empty = el.querySelector('.chat-empty, [style*="chat_bubble_outline"]');
    if (empty) empty.remove();

    const isMine = resolveMessageIsMine(m);
    const initial = (m.senderName || 'U').charAt(0).toUpperCase();
    const ctx = isMine
        ? `<div class="msg-ctx-menu" id="ctx-${m.id}"><div class="ctx-item danger" onclick="openDeleteModal('${m.id}')"><span class="material-icons-round" style="font-size:16px;">delete</span>Xóa</div></div>`
        : `<div class="msg-ctx-menu" id="ctx-${m.id}"><div class="ctx-item danger" onclick="openReportModal('${m.id}')"><span class="material-icons-round" style="font-size:16px;">flag</span>Báo cáo</div></div>`;

    const row = document.createElement('div');
    row.className = `msg-row ${isMine ? 'mine' : ''}`;
    row.id = `msg-${m.id}`;
    row.innerHTML = `
        ${!isMine ? `<div class="msg-avatar">${initial}</div>` : ''}
        <div style="display:flex;flex-direction:column;gap:3px;position:relative;">
            <div class="msg-bubble" oncontextmenu="openCtxMenu(event,'${m.id}')" ondblclick="openCtxMenu(event,'${m.id}')">${Number(m.messageType) === 4 ? renderHiringOfferCard(m, isMine) : esc(m.content)}${ctx}</div>
            <div class="msg-time">${fmtMsgTime(m.createdAt)}</div>
        </div>
        ${isMine ? `<div class="msg-avatar">${initial}</div>` : ''}`;
    el.appendChild(row);
    el.scrollTop = el.scrollHeight;
}

// ════════════════════════════════════════════════════════════════════
//  SEND MESSAGE
// ════════════════════════════════════════════════════════════════════
async function sendMessage() {
    const ta = document.getElementById('msgInput');
    const text = ta.value.trim();
    if (!text || !activeConvId) return;

    try {
        const hubResult = await sendMessageViaHub(text);
        if (hubResult.sent) {
            ta.value = '';
            ta.style.height = 'auto';
        } else if (hubResult.rateLimited) {
            // text vẫn còn trong ta vì chưa xóa → hiện lỗi inline
            showChatNotice('Bạn gửi tin nhắn quá nhanh. Vui lòng thử lại sau.', 'error');
        } else {
            // Fallback HTTP
            ta.value = '';
            ta.style.height = 'auto';
            const res = await fetch(`/Communication/SendMessage?conversationId=${activeConvId}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgery() },
                body: JSON.stringify({ content: text, messageType: 1 })
            });
            if (res.status === 429) {
                ta.value = text;
                showChatNotice('Bạn gửi tin nhắn quá nhanh. Vui lòng thử lại sau.', 'error');
                return;
            }
            const json = await res.json();
            if (json.success) {
                appendMessage(json.data);
                updateConversationPreview(json.data, true);
            }
        }
    } catch {}
}

// ════════════════════════════════════════════════════════════════════
//  BLOCK / HIDE
// ════════════════════════════════════════════════════════════════════
async function toggleBlock() {
    if (!activeConvId || !activeConvData) return;
    if (activeConvData.isBlocked && !canCurrentUserUnblock()) {
        alert('Chỉ người đã khóa cuộc trò chuyện mới có thể mở khóa.');
        return;
    }
    const action = activeConvData.isBlocked ? 'unblock' : 'block';
    const status = await updateStatus(action);
    if (!status) return;
    activeConvData.isBlocked = !!status.isBlocked;
    activeConvData.isHidden = !!status.isHidden;
    activeConvData.blockedByUserId = status.blockedByUserId || null;
    updateBlockUI(activeConvData.isBlocked);
}

async function unblockConv() {
    if (!canCurrentUserUnblock()) {
        alert('Chỉ người đã khóa cuộc trò chuyện mới có thể mở khóa.');
        return;
    }
    const status = await updateStatus('unblock');
    if (!status) return;
    activeConvData.isBlocked = !!status.isBlocked;
    activeConvData.isHidden = !!status.isHidden;
    activeConvData.blockedByUserId = status.blockedByUserId || null;
    updateBlockUI(activeConvData.isBlocked);
}

async function hideConv() {
    if (!activeConvId) return;
    const action = activeConvData?.isHidden ? 'unhide' : 'hide';
    const status = await updateStatus(action);
    if (!status) return;
    activeConvData.isHidden = !!status.isHidden;
    if (action === 'hide') {
        alert('Hội thoại đã được ẩn.');
        allConversations = allConversations.filter(c => String(c.id) !== String(activeConvId));
        renderConversations(allConversations);
        activeConvId = null;
        activeConvData = null;
        document.getElementById('chatArea').style.display = 'none';
        document.getElementById('chatEmpty').style.display = 'flex';
    }
}

async function updateStatus(action) {
    try {
        const res = await fetch(`/Communication/UpdateStatus/${activeConvId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgery() },
            body: JSON.stringify({ action })
        });
        const json = await res.json();
        if (!res.ok || !json?.success) {
            alert(json?.message || 'Không thể cập nhật trạng thái hội thoại.');
            return null;
        }
        return json.data ?? null;
    } catch {}
    return null;
}

function canCurrentUserUnblock() {
    if (!activeConvData?.isBlocked) return true;
    return !!activeConvData.blockedByUserId && String(activeConvData.blockedByUserId) === String(currentUserId);
}

function updateBlockUI(isBlocked) {
    const banner = document.getElementById('blockedBanner');
    const inputArea = document.getElementById('chatInputArea');
    const blockBtn = document.getElementById('btnBlockConv');
    const canUnblock = canCurrentUserUnblock();
    const blockIcon = '<span class="material-icons-round" style="font-size:18px;">block</span>';

    if (isBlocked) {
        if (banner) banner.style.display = 'flex';
        if (inputArea) inputArea.style.display = 'none';
        if (blockBtn) {
            blockBtn.innerHTML = blockIcon;
            blockBtn.title = canUnblock ? 'Mở khóa hội thoại' : 'Đã khóa — chỉ người khóa mới có thể mở khóa';
            blockBtn.setAttribute('aria-label', canUnblock ? 'Mở khóa hội thoại' : 'Không thể mở khóa');
            blockBtn.disabled = !canUnblock;
        }
    } else {
        if (banner) banner.style.display = 'none';
        if (inputArea) inputArea.style.display = 'flex';
        if (blockBtn) {
            blockBtn.innerHTML = blockIcon;
            blockBtn.title = 'Khóa hội thoại';
            blockBtn.setAttribute('aria-label', 'Khóa hội thoại');
            blockBtn.disabled = false;
        }
    }
}

// ════════════════════════════════════════════════════════════════════
//  CONTEXT MENU
// ════════════════════════════════════════════════════════════════════
function openCtxMenu(e, msgId) {
    e.preventDefault();
    e.stopPropagation();
    document.querySelectorAll('.msg-ctx-menu.open').forEach(m => m.classList.remove('open'));
    document.getElementById(`ctx-${msgId}`)?.classList.add('open');
}

// ════════════════════════════════════════════════════════════════════
//  DELETE
// ════════════════════════════════════════════════════════════════════
function openDeleteModal(msgId) {
    pendingDeleteMsgId = msgId;
    document.getElementById('deleteModal').style.display = 'flex';
}

function closeDeleteModal() {
    document.getElementById('deleteModal').style.display = 'none';
    pendingDeleteMsgId = null;
}

async function confirmDelete() {
    if (!pendingDeleteMsgId) return;
    try {
        const res = await fetch(`/Communication/DeleteMessage/${pendingDeleteMsgId}`, {
            method: 'DELETE',
            headers: { 'RequestVerificationToken': getAntiForgery() }
        });
        const json = await res.json();
        if (json.success) applyMessageDeleted(pendingDeleteMsgId, activeConvId);
    } catch {}
    closeDeleteModal();
}

// ════════════════════════════════════════════════════════════════════
//  REPORT
// ════════════════════════════════════════════════════════════════════
function openReportModal(msgId) {
    pendingReportMsgId = msgId;
    document.getElementById('reportModal').style.display = 'flex';
    document.getElementById('reportReason').value = '';
    document.getElementById('reportEvidence').value = '';
}

function closeReportModal() {
    document.getElementById('reportModal').style.display = 'none';
    pendingReportMsgId = null;
}

async function submitReport() {
    const reason = document.getElementById('reportReason').value.trim();
    if (!reason) { alert('Vui lòng nhập lý do báo cáo.'); return; }
    try {
        await fetch(`/Communication/ReportMessage/${pendingReportMsgId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgery() },
            body: JSON.stringify({ reason, evidence: document.getElementById('reportEvidence').value.trim() })
        });
        alert('Báo cáo đã được gửi. Cảm ơn bạn đã giúp giữ gìn cộng đồng.');
    } catch {}
    closeReportModal();
}

// ════════════════════════════════════════════════════════════════════
//  UTILS
// Hiện thông báo inline phía trên ô nhập — tự biến mất sau 3 giây
function showChatNotice(message, type = 'error') {
    const area = document.getElementById('chatInputArea');
    if (!area) return;
    const existing = document.getElementById('chatNotice');
    if (existing) existing.remove();

    const el = document.createElement('div');
    el.id = 'chatNotice';
    el.textContent = message;
    el.style.cssText = `
        padding: 8px 14px; font-size: 13px; border-radius: 10px; margin: 4px 12px;
        background: ${type === 'error' ? '#fef2f2' : '#f0fdf4'};
        color: ${type === 'error' ? '#dc2626' : '#16a34a'};
        border: 1px solid ${type === 'error' ? '#fecaca' : '#bbf7d0'};
    `;
    area.insertAdjacentElement('beforebegin', el);
    setTimeout(() => el.remove(), 3000);
}

// ════════════════════════════════════════════════════════════════════
function esc(v) {
    return String(v ?? '')
        .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;').replaceAll("'", '&#39;');
}

function fmtTime(v) {
    if (!v) return '';
    const d = new Date(v);
    const diff = (new Date() - d) / 1000;
    if (diff < 60) return 'Vừa xong';
    if (diff < 3600) return `${Math.floor(diff / 60)} phút`;
    if (diff < 86400) return `${Math.floor(diff / 3600)} giờ`;
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' });
}

function fmtMsgTime(v) {
    if (!v) return '';
    return new Date(v).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
}

function getAntiForgery() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
}

function resolveMessageIsMine(message) {
    if (activeConvData?.otherUserId && message?.senderId) {
        return String(message.senderId) !== String(activeConvData.otherUserId);
    }
    if (typeof message?.isMine === 'boolean') return message.isMine;
    return false;
}

function applyMessageDeleted(messageId, conversationId) {
    const bubble = document.querySelector(`#msg-${messageId} .msg-bubble`);
    if (bubble) {
        bubble.textContent = '[Tin nhắn đã bị xóa]';
        bubble.classList.add('deleted');
    }
    if (conversationId) {
        updateConversationPreview(
            { conversationId, content: '[Tin nhắn đã bị xóa]', createdAt: new Date().toISOString() },
            true
        );
    }
}

async function goToContractDetailByHiringRecord(hiringRecordId) {
    if (!hiringRecordId) return;
    window.open(`/Contract/ViewContractDetail?hiringRecordId=${encodeURIComponent(hiringRecordId)}`, '_blank', 'noopener');
}

function renderHiringOfferCard(message, isMine) {
    const hiringRecordId = message.attachmentUrl || '';
    return `
    <div class="hiring-offer-card" onclick="goToContractDetailByHiringRecord('${esc(hiringRecordId)}')" title="Bam de xem chi tiet de nghi viec lam">
        <div class="hiring-offer-card__header">
            <span class="material-icons-round" style="font-size:20px;">handshake</span>
            <span>Đề nghị việc làm</span>
        </div>
        <div class="hiring-offer-card__body">
            <p class="hiring-offer-card__label">
                ${isMine ? 'Bạn đã gửi đề nghị thuê bảo mẫu.' : 'Bạn nhận được đề nghị việc làm!'}
            </p>
            <p class="hiring-offer-card__hint">${isMine ? 'Bấm để xem chi tiết đề nghị việc làm.' : 'Bấm để xem đề nghị và phản hồi.'}</p>
        </div>
        <div class="hiring-offer-card__footer">
            <span class="material-icons-round" style="font-size:16px;opacity:.8;">open_in_new</span>
            Xem chi tiết đề nghị việc làm
        </div>
    </div>`;
}

function buildMessageSignature(messages) {
    if (!Array.isArray(messages) || !messages.length) return 'empty';
    return messages.map(m => `${m.id || ''}|${m.readAt || ''}|${m.isDeleted ? 1 : 0}|${m.createdAt || ''}`).join('~');
}

async function syncConversationGroups() {
    if (!hubConnection || hubConnection.state !== signalR.HubConnectionState.Connected) return;
    if (isSyncingConversationGroups) return;
    isSyncingConversationGroups = true;
    try {
        const wantedConversationIds = new Set(
            (allConversations || []).map(c => String(c?.id || '')).filter(Boolean)
        );
        for (const joinedId of Array.from(joinedConversationIds)) {
            if (wantedConversationIds.has(joinedId)) continue;
            await hubConnection.invoke('LeaveConversation', joinedId);
            joinedConversationIds.delete(joinedId);
        }
        for (const convId of wantedConversationIds) {
            if (joinedConversationIds.has(convId)) continue;
            await hubConnection.invoke('JoinConversation', convId);
            joinedConversationIds.add(convId);
        }
    } catch {
        // no-op
    } finally {
        isSyncingConversationGroups = false;
    }
}

function startRealtimeSync() {
    stopRealtimeSync();
    conversationsPollTimer = window.setInterval(() => loadConversations(true), CONVERSATIONS_POLL_INTERVAL_MS);
    messagesPollTimer = window.setInterval(() => {
        if (!activeConvId) return;
        loadMessages(activeConvId, 1, true);
    }, MESSAGES_POLL_INTERVAL_MS);
}

function stopRealtimeSync() {
    if (conversationsPollTimer) { window.clearInterval(conversationsPollTimer); conversationsPollTimer = null; }
    if (messagesPollTimer) { window.clearInterval(messagesPollTimer); messagesPollTimer = null; }
}

// ════════════════════════════════════════════════════════════════════
//  SIGNALR
// ════════════════════════════════════════════════════════════════════
async function initSignalR() {
    if (!apiBaseUrl || typeof signalR === 'undefined') return;
    const token = window.ChatConfig?.accessToken ?? '';
    if (!token) return;

    hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(`${apiBaseUrl}/hubs/chat`, { accessTokenFactory: () => token })
        .withAutomaticReconnect()
        .build();

    hubConnection.on('ReceiveMessage', async (message) => {
        if (!message || !message.conversationId) return;
        const isMine = resolveMessageIsMine(message);
        if (String(message.conversationId) === String(activeConvId)) {
            appendMessage({ ...message, isMine });
            updateConversationPreview(message, isMine);
            if (!isMine) await markConversationReadOnServer(message.conversationId);
        } else {
            updateConversationPreview(message, isMine);
            notifyHeaderChatUnread();
        }
    });

    hubConnection.on('ConversationStatusChanged', status => {
        if (!status || !status.conversationId || !activeConvId || !activeConvData) return;
        if (String(status.conversationId) !== String(activeConvId)) return;
        activeConvData.isBlocked = !!status.isBlocked;
        activeConvData.blockedByUserId = status.blockedByUserId || null;
        updateBlockUI(activeConvData.isBlocked);
    });

    hubConnection.on('MessageDeleted', data => {
        if (!data?.messageId) return;
        applyMessageDeleted(data.messageId, data.conversationId);
    });

    hubConnection.on('Error', msg => {
        console.warn('SignalR error:', msg);
        if (!msg) return;
        if (msg.includes('quyền truy cập')) {
            joinedConversationIds.delete(activeConvId);
        }
        // Rate limit được xử lý trực tiếp trong catch của sendMessageViaHub
    });

    hubConnection.onreconnected(async () => {
        joinedConversationIds.clear();
        await loadConversations(true);
        await syncConversationGroups();
        if (activeConvId) await loadMessages(activeConvId, 1, true);
    });

    try {
        await hubConnection.start();
        await syncConversationGroups();
    } catch (err) {
        console.warn('SignalR connect failed:', err);
    }
}

// Trả về { sent: true } | { sent: false, rateLimited: true } | { sent: false }
async function sendMessageViaHub(text) {
    if (!hubConnection || hubConnection.state !== signalR.HubConnectionState.Connected)
        return { sent: false };
    try {
        await hubConnection.invoke('SendMessage', activeConvId, text, 1);
        return { sent: true };
    } catch (err) {
        const msg = err?.message ?? '';
        if (msg.includes('quá nhanh')) return { sent: false, rateLimited: true };
        return { sent: false };
    }
}

function updateConversationPreview(message, isMine) {
    const conv = allConversations.find(c => String(c.id) === String(message.conversationId));
    if (!conv) return;
    conv.lastMessage = message.content || conv.lastMessage;
    conv.lastMessageAt = message.createdAt || new Date().toISOString();
    if (!isMine && String(message.conversationId) !== String(activeConvId)) {
        conv.unreadCount = (conv.unreadCount || 0) + 1;
    }
    renderConversations(allConversations);
}

function resetUnreadCount(convId) {
    const conv = allConversations.find(c => String(c.id) === String(convId));
    if (!conv) return;
    conv.unreadCount = 0;
    renderConversations(allConversations);
}
