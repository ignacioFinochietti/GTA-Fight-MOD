const express = require('express');
const http = require('http');
const WebSocket = require('ws');
const cors = require('cors');
const { TikTokLiveConnection } = require('tiktok-live-connector');

const app = express();
app.use(cors());
app.use(express.json());
app.use(express.static('overlay'));

const server = http.createServer(app);
const wss = new WebSocket.Server({ server });

let pendingEvents = [];
let leaderboard = {};
let maxPedsCap = 7;
let wallsVisible = true;

let TIKTOK_USERNAME = 'tu_usuario_tiktok';
let isTikTokConnected = false;
let tiktokLiveConnection = null;

wss.on('connection', (ws) => {
    console.log('[WS] Cliente Overlay conectado a TikTok Live NPC Battles');
    ws.send(JSON.stringify({ type: 'LEADERBOARD_UPDATE', data: getSortedLeaderboard() }));
});

function broadcast(data) {
    const payload = JSON.stringify(data);
    wss.clients.forEach((client) => {
        if (client.readyState === WebSocket.OPEN) {
            client.send(payload);
        }
    });
}

function getSortedLeaderboard() {
    return Object.keys(leaderboard)
        .map(donor => ({ name: donor, totalGifts: leaderboard[donor] }))
        .sort((a, b) => b.totalGifts - a.totalGifts)
        .slice(0, 10);
}

// CONEXIÓN AUTOMÁTICA EN BUCLE A TIKTOK LIVE
function startTikTokConnector() {
    if (tiktokLiveConnection) {
        try { tiktokLiveConnection.disconnect(); } catch (e) {}
    }

    const cleanUser = TIKTOK_USERNAME.replace(/https?:\/\/(www\.)?tiktok\.com\/@?/i, '').replace(/[^a-zA-Z0-9._]/g, '').trim();
    if (!cleanUser) return;

    tiktokLiveConnection = new TikTokLiveConnection(cleanUser, {});

    tiktokLiveConnection.connect().then(state => {
        isTikTokConnected = true;
        console.log(`[TIKTOK LIVE] ¡TRANSMISIÓN EN VIVO DETECTADA! Conectado exitosamente a @${cleanUser} (Room ID: ${state.roomId})`);
    }).catch(err => {
        isTikTokConnected = false;
        console.log(`[TIKTOK LIVE] Esperando a que @${cleanUser} inicie transmisión EN VIVO en TikTok... (Reintentando automático en 10s). ${err.message || ''}`);
        setTimeout(startTikTokConnector, 10000);
    });

function extractUsername(data) {
    if (!data) return '@Viewer';
    const uid = data.uniqueId || (data.user && data.user.uniqueId) || (data.userDetails && data.userDetails.uniqueId);
    if (uid) return `@${uid}`;
    const nick = data.nickname || (data.user && data.user.nickname) || (data.userDetails && data.userDetails.nickname);
    if (nick) return `@${nick}`;
    return '@Viewer';
}

    tiktokLiveConnection.on('gift', data => {
        if (data.giftType === 1 && !data.repeatEnd) return;

        const donor = extractUsername(data);
        const giftName = data.giftName || 'Rose';
        const repeatCount = data.repeatCount || 1;

        console.log(`[REGALO REGISTRADO EN VIVO] ${donor} envió ${repeatCount}x ${giftName}`);

        let unitType = 'standard';
        let weapon = 'PISTOL';
        const lower = giftName.toLowerCase();

        if (lower.includes('rose') || lower.includes('rosa')) { unitType = 'gangster'; weapon = 'MICROSMG'; }
        else if (lower.includes('ice cream') || lower.includes('helado')) { unitType = 'swat'; weapon = 'CARBINERIFLE'; }
        else if (lower.includes('donut') || lower.includes('dona')) { unitType = 'alien'; weapon = 'RAYPISTOL'; }
        else if (lower.includes('rocket') || lower.includes('cohete')) { unitType = 'juggernaut'; weapon = 'MINIGUN'; }
        else if (lower.includes('hat') || lower.includes('sombrero')) { unitType = 'sniper'; weapon = 'SNIPER'; }
        else if (lower.includes('heart') || lower.includes('corazon')) { unitType = 'ninja'; weapon = 'KATANA'; }
        else if (lower.includes('lion') || lower.includes('leon')) { unitType = 'boss'; weapon = 'RPG'; }
        else if (lower.includes('universe') || lower.includes('universo') || lower.includes('container')) { unitType = 'container'; weapon = 'NONE'; }

        for (let i = 0; i < repeatCount; i++) {
            pendingEvents.push({
                team: donor,
                unitType: unitType,
                weapon: weapon,
                donor: donor,
                timestamp: Date.now()
            });
        }

        leaderboard[donor] = (leaderboard[donor] || 0) + repeatCount;
        broadcast({ type: 'GIFT_RECEIVED', donor, giftName, team: donor, count: repeatCount });
        broadcast({ type: 'LEADERBOARD_UPDATE', data: getSortedLeaderboard() });
    });

    let likeCountTracker = {};
    tiktokLiveConnection.on('like', data => {
        const donor = extractUsername(data);
        const likesReceived = data.likeCount || 1;
        likeCountTracker[donor] = (likeCountTracker[donor] || 0) + likesReceived;

        console.log(`[TAPS EN VIVO] ${donor} dio ${likesReceived} taps (Acumulado: ${likeCountTracker[donor]}/2)`);

        if (likeCountTracker[donor] >= 2) {
            const spawns = Math.floor(likeCountTracker[donor] / 2);
            likeCountTracker[donor] %= 2;

            for (let i = 0; i < spawns; i++) {
                pendingEvents.push({
                    team: donor,
                    unitType: 'brawler',
                    weapon: 'BAT',
                    donor: donor,
                    timestamp: Date.now()
                });
            }

            leaderboard[donor] = (leaderboard[donor] || 0) + spawns;
            broadcast({ type: 'GIFT_RECEIVED', donor, giftName: 'Bate de Béisbol (2 Taps)', team: donor, count: spawns });
            broadcast({ type: 'LEADERBOARD_UPDATE', data: getSortedLeaderboard() });
        }
    });

    tiktokLiveConnection.on('error', err => {
        if (err && (err.name === 'UserOfflineError' || (err.exception && err.exception.name === 'UserOfflineError'))) return;
        console.log(`[TIKTOK LIVE INFO] ${err.message || 'Esperando directo...'}`);
    });

    tiktokLiveConnection.on('streamEnd', () => {
        isTikTokConnected = false;
        console.log(`[TIKTOK LIVE] Transmisión finalizada. Esperando nuevo Live...`);
        setTimeout(startTikTokConnector, 10000);
    });
}

// INICIAR CONECTOR TIKTOK LIVE
startTikTokConnector();

// ENDPOINT POLLING GTA V
let activePlatform = 'tiktok';
let youtubeLiveId = '';

app.get('/api/pending-events', (req, res) => {
    const eventsToReturn = [...pendingEvents];
    pendingEvents = [];
    res.json({ status: 'ok', events: eventsToReturn });
});

// GET & POST CONFIGURACIÓN DE PARÁMETROS
app.get('/api/config', (req, res) => {
    res.json({
        platform: activePlatform,
        tiktokUsername: TIKTOK_USERNAME,
        youtubeLiveId: youtubeLiveId,
        maxPedsCap: maxPedsCap,
        wallsVisible: wallsVisible,
        isTikTokConnected: isTikTokConnected
    });
});

app.post('/api/config', (req, res) => {
    const { platform, tiktokUsername: newUsername, maxPedsCap: newCap } = req.body;
    if (platform) activePlatform = platform;
    if (newCap) {
        maxPedsCap = Math.max(1, Math.min(20, parseInt(newCap)));
        pendingEvents.push({ unitType: 'set_ped_cap', limit: maxPedsCap, donor: 'SYSTEM' });
    }
    if (newUsername) {
        const cleaned = newUsername.replace(/https?:\/\/(www\.)?tiktok\.com\/@?/i, '').replace(/[^a-zA-Z0-9._]/g, '').trim();
        if (cleaned && cleaned !== TIKTOK_USERNAME) {
            TIKTOK_USERNAME = cleaned;
            startTikTokConnector();
        }
    }
    broadcast({ type: 'CONFIG_UPDATED', platform: activePlatform, maxPedsCap, tiktokUsername: TIKTOK_USERNAME });
    res.json({ success: true, activePlatform, maxPedsCap, tiktokUsername: TIKTOK_USERNAME });
});

// ENDPOINT YOUTUBE LIVE TRIGGER (SUPER CHAT & MEMBERS)
app.get('/api/youtube/trigger', (req, res) => {
    const donor = req.query.donor || '@YTFan';
    const type = req.query.type || 'SuperChat';
    const amount = parseFloat(req.query.amount) || 5;
    const giftName = req.query.gift || 'Rocket';

    let unitType = 'standard';
    let weapon = 'PISTOL';
    const lower = giftName.toLowerCase();

    if (lower.includes('swat') || amount <= 1) { unitType = 'swat'; weapon = 'CARBINERIFLE'; }
    else if (lower.includes('rocket') || amount <= 2.5) { unitType = 'juggernaut'; weapon = 'MINIGUN'; }
    else if (lower.includes('hat') || amount <= 5) { unitType = 'sniper'; weapon = 'SNIPER'; }
    else if (lower.includes('heart') || amount <= 10) { unitType = 'ninja'; weapon = 'KATANA'; }
    else if (lower.includes('lion') || amount <= 25) { unitType = 'boss'; weapon = 'RPG'; }
    else if (lower.includes('universe') || amount >= 50) { unitType = 'container'; weapon = 'NONE'; }

    pendingEvents.push({
        team: donor,
        unitType: unitType,
        weapon: weapon,
        donor: donor,
        timestamp: Date.now()
    });

    leaderboard[donor] = (leaderboard[donor] || 0) + (amount > 0 ? Math.floor(amount) : 1);
    broadcast({ type: 'GIFT_RECEIVED', donor, giftName: `YouTube ${type} ($${amount})`, team: donor, count: 1 });
    broadcast({ type: 'LEADERBOARD_UPDATE', data: getSortedLeaderboard() });

    res.json({ success: true, message: `YouTube SuperChat $${amount} procesado para ${donor}` });
});

app.post('/api/youtube/connect', (req, res) => {
    const { liveId } = req.body;
    youtubeLiveId = liveId || '';
    console.log(`[YOUTUBE LIVE] ID de Chat / Transmisión configurado a: ${youtubeLiveId}`);
    broadcast({ type: 'YOUTUBE_CONNECTED', liveId: youtubeLiveId });
    res.json({ success: true, youtubeLiveId });
});

// ENDPOINTS DE PRUEBA MANUAL TIKTOK
app.get('/api/test-gift', (req, res) => {
    const giftName = req.query.gift || 'Rose';
    const donor = req.query.donor || '@TikTokFan';

    let unitType = 'standard';
    let weapon = 'PISTOL';
    const lower = giftName.toLowerCase();

    if (lower.includes('rose') || lower.includes('rosa')) { unitType = 'gangster'; weapon = 'MICROSMG'; }
    else if (lower.includes('ice cream') || lower.includes('helado')) { unitType = 'swat'; weapon = 'CARBINERIFLE'; }
    else if (lower.includes('donut') || lower.includes('dona')) { unitType = 'alien'; weapon = 'RAYPISTOL'; }
    else if (lower.includes('rocket') || lower.includes('cohete')) { unitType = 'juggernaut'; weapon = 'MINIGUN'; }
    else if (lower.includes('hat') || lower.includes('sombrero')) { unitType = 'sniper'; weapon = 'SNIPER'; }
    else if (lower.includes('heart') || lower.includes('corazon')) { unitType = 'ninja'; weapon = 'KATANA'; }
    else if (lower.includes('lion') || lower.includes('leon')) { unitType = 'boss'; weapon = 'RPG'; }
    else if (lower.includes('universe') || lower.includes('universo') || lower.includes('container')) { unitType = 'container'; weapon = 'NONE'; }

    pendingEvents.push({
        team: donor,
        unitType: unitType,
        weapon: weapon,
        donor: donor,
        timestamp: Date.now()
    });

    leaderboard[donor] = (leaderboard[donor] || 0) + 1;
    broadcast({ type: 'GIFT_RECEIVED', donor, giftName, team: donor, count: 1 });
    broadcast({ type: 'LEADERBOARD_UPDATE', data: getSortedLeaderboard() });

    res.json({ success: true, message: `Evento ${giftName} enviado para ${donor}` });
});

app.get('/api/tap', (req, res) => {
    const donor = req.query.donor || '@BateadorFan';

    pendingEvents.push({
        team: donor,
        unitType: 'brawler',
        weapon: 'BAT',
        donor: donor,
        timestamp: Date.now()
    });

    leaderboard[donor] = (leaderboard[donor] || 0) + 1;
    broadcast({ type: 'GIFT_RECEIVED', donor, giftName: 'Bate de Béisbol (10 Taps)', team: donor, count: 1 });

    res.json({ success: true, message: `Bateador enviado para ${donor}` });
});

app.get('/api/toggle-walls', (req, res) => {
    wallsVisible = req.query.visible === 'true' ? true : (req.query.visible === 'false' ? false : !wallsVisible);
    pendingEvents.push({
        unitType: 'toggle_walls',
        visible: wallsVisible,
        donor: 'SYSTEM'
    });
    res.json({ success: true, wallsVisible });
});

app.get('/api/set-ped-cap', (req, res) => {
    const cap = parseInt(req.query.limit) || 6;
    maxPedsCap = Math.max(1, Math.min(20, cap));
    pendingEvents.push({
        unitType: 'set_ped_cap',
        limit: maxPedsCap,
        donor: 'SYSTEM'
    });
    res.json({ success: true, maxPedsCap });
});

const PORT = 3000;
server.listen(PORT, () => {
    console.log(`[SERVER] Servidor TikTok/YouTube Live listo en http://localhost:${PORT}`);
    console.log(`[SERVER] Panel de Control / Configuración disponible en http://localhost:${PORT}/admin.html`);
});

