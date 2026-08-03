const WS_URL = 'ws://localhost:3000';
let socket;
const leaderboardData = {};

function connectWebSocket() {
    socket = new WebSocket(WS_URL);

    socket.onopen = () => {
        console.log('[Overlay WS] Conectado al servidor de TikTok Live/GTA V');
    };

    socket.onmessage = (event) => {
        try {
            const payload = JSON.parse(event.data);
            handleServerMessage(payload);
        } catch (err) {
            console.error('[Overlay WS] Error parseando mensaje:', err);
        }
    };

    socket.onclose = () => {
        setTimeout(connectWebSocket, 3000);
    };
}

function handleServerMessage(payload) {
    if (payload.type === 'GIFT_RECEIVED') {
        addGiftCard(payload);
        updateLeaderboard(payload.donor, payload.count || 1);
    }
}

function addGiftCard(giftData) {
    const feed = document.getElementById('gift-feed');
    if (!feed) return;

    const card = document.createElement('div');
    card.className = 'feed-item';

    let giftEmoji = '🎁';
    const lower = (giftData.giftName || '').toLowerCase();
    if (lower.includes('rose') || lower.includes('rosa')) giftEmoji = '🌹';
    else if (lower.includes('ice cream') || lower.includes('helado')) giftEmoji = '🍦';
    else if (lower.includes('donut') || lower.includes('dona')) giftEmoji = '🍩';
    else if (lower.includes('rocket') || lower.includes('cohete')) giftEmoji = '🚀';
    else if (lower.includes('hat') || lower.includes('sombrero')) giftEmoji = '🎩';
    else if (lower.includes('heart') || lower.includes('corazon')) giftEmoji = '💖';
    else if (lower.includes('lion') || lower.includes('leon')) giftEmoji = '🦁';
    else if (lower.includes('universe') || lower.includes('universo') || lower.includes('container')) giftEmoji = '🌌';
    else if (lower.includes('bate') || lower.includes('tap')) giftEmoji = '👆';

    const donorName = giftData.donor || '@Viewer';
    const giftName = giftData.giftName || 'Regalo';
    const count = giftData.count || 1;

    card.innerHTML = `
        <div class="icon">${giftEmoji}</div>
        <div class="details">
            <span class="user">${escapeHtml(donorName)}</span>
            <span class="action">Envió ${count}x ${escapeHtml(giftName)}</span>
        </div>
    `;

    feed.prepend(card);

    if (feed.children.length > 5) {
        feed.removeChild(feed.lastChild);
    }
}

function updateLeaderboard(donorName, count) {
    if (!donorName) return;
    leaderboardData[donorName] = (leaderboardData[donorName] || 0) + count;
    renderLeaderboard();
}

function renderLeaderboard() {
    const list = document.getElementById('leaderboard');
    if (!list) return;

    list.innerHTML = '';

    const sorted = Object.keys(leaderboardData)
        .map(key => ({ name: key, count: leaderboardData[key] }))
        .sort((a, b) => b.count - a.count)
        .slice(0, 5);

    if (sorted.length === 0) {
        list.innerHTML = '<li class="empty-msg">Envía un regalo para aparecer aquí...</li>';
        return;
    }

    sorted.forEach((item, index) => {
        const li = document.createElement('li');
        li.className = 'leaderboard-item';
        li.innerHTML = `
            <span class="rank">#${index + 1}</span>
            <div class="user-info">
                <span class="username">${escapeHtml(item.name)}</span>
                <span class="score">${item.count} Aportes</span>
            </div>
        `;
        list.appendChild(li);
    });
}

function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

window.addEventListener('DOMContentLoaded', () => {
    connectWebSocket();
});
