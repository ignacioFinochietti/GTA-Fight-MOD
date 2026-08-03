# Stream to Earn - TikTok & YouTube Live NPC Battles (GTA V)

Sistema interactivo para transmisiones en vivo que convierte regalos y acciones del chat de TikTok Live (y opcionalmente YouTube Live) en batallas de NPCs y eventos en tiempo real dentro de **GTA V**.

---

## 🏗️ Arquitectura del Sistema

```
[ TikTok Live (@tu_usuario_tiktok) / YouTube Live Chat ]
                    │
                    ▼
[ Node.js + Express + WebSocket Server (Puerto 3000) ]
       │                                     │
       ▼                                     ▼
[ Overlay Web (OBS / 9:16) ]         [ Queue /api/pending-events ]
   (HUD, VS, Top Donantes, Feed)             │
                                             ▼
                               [ GTA V C# ScriptHookVDotNet ]
                               (Spawnea NPCs, Armas, Arenas, Explosiones)
```

---

## 📁 Estructura del Proyecto

1. **`server/` (Servidor Node.js + Express + WebSockets)**
   - **`index.js`**: 
     - Conexión automática con `tiktok-live-connector` al usuario `@tu_usuario_tiktok`.
     - Recibe regalos (`gift`) y me gustas (`like`) y los traduce a tipos de unidades (`unitType`) y armas (`weapon`).
     - Almacena una cola en memoria (`pendingEvents`).
     - Servidor WebSocket para actualizar el Overlay Web en tiempo real.
     - Endpoints REST `/api/*` para consumo de GTA V y pruebas manuales.

2. **`overlay/` (Interfaz Web Transparente para OBS Studio / TikTok Studio)**
   - **`index.html` / `styles.css` / `app.js`**:
     - **Barra Superior (Header HUD)**: Título *LAST STANDING*, usuario activo, badge *EN VIVO*, barra de duelo VS y ticker de regalos.
     - **Centro (Game Viewport)**: 100% transparente para mostrar el juego GTA V de fondo.
     - **Bloque Inferior (Footer HUD)**: Tabla de donadores (*Top Donadores*), feed de actividad reciente, catálogo de 9 regalos y panel tester de pruebas flotante.
   - **`tiktok_live.html` / `tiktok_style.css`**: Versión vertical alternativa optimizada en 9:16 (1080x1920).

3. **`gta_script/` (Script C# para GTA V)**
   - **`TikTokNPCBattles3.cs`**:
     - Script ejecutable en GTA V via ScriptHookVDotNet.
     - Polling continuo a `http://localhost:3000/api/pending-events`.
     - Creación de arena circular física (`ARENA_SIZE`) con barreras invisibles.
     - Spawnea Peds/NPCs de cada donador asignando equipos, colores, salud (HP), armadura y armas.
     - Eventos especiales como caída de contenedores pesados con explosiones en cadena (`container`).
     - Sistema de cámara orbital automatizada y visualización de nombres/barras de vida sobre cada NPC.

4. **`iniciar_servidor.bat`**
   - Script batch ejecutable para arrancar el servidor Node.js en el puerto 3000 de forma rápida.

---

## 🎮 Mapeo de Items y Regalos

| Regalo / Acción | Unidad (`unitType`) | Arma (`weapon`) | Modelo NPC / Efecto | HP / Armadura |
| :--- | :--- | :--- | :--- | :--- |
| **🌹 Rosa** | `gangster` | `MICROSMG` | `g_m_y_ballaeast_01` (Balla) | Standard |
| **🍦 Helado** | `swat` | `CARBINERIFLE` | `s_m_y_swat_01` (SWAT Táctico) | Standard |
| **🍩 Dona** | `alien` | `RAYPISTOL` | `u_m_y_zombie_01` (Alien/Zombi) | Standard |
| **🚀 Cohete** | `juggernaut` | `MINIGUN` | `s_m_m_ciasec_01` (Pesado CIA) | 600 HP / 200 Armor |
| **🎩 Sombrero** | `sniper` | `SNIPER` | `s_m_y_sheriff_01` (Sheriff) | Standard |
| **💖 Corazón** | `ninja` | `KATANA` | `g_m_y_korean_01` (Ninja) | 300 HP |
| **🦁 León** | `boss` | `RPG` | Add-on Skin `bob` / `s_m_m_movalien_01` | 1000 HP / 300 Armor |
| **🌌 Universo** | `container` | `NONE` | Cae `prop_container_01a` con explosión masiva | Impacto de área |
| **👆 10 Taps** | `brawler` | `BAT` | `g_m_y_salvaboss_01` (Matón) | 220 HP / 50 Armor |

---

## 📡 Referencia de API Endpoints (`http://localhost:3000`)

- `GET /api/pending-events` -> Devuelve lista de eventos acumulados y limpia la cola para GTA V.
- `GET /api/test-gift?gift=Rose&donor=@Usuario` -> Simula un regalo desde el panel tester.
- `GET /api/tap?donor=@Usuario` -> Simula 10 me gustas (spawnea bateador).
- `GET /api/toggle-walls?visible=true|false` -> Alterna visibilidad de las paredes de la arena en GTA V.
- `GET /api/set-ped-cap?limit=6` -> Ajusta límite máximo de NPCs activos por donador.

---

## 🔴 Configuración YouTube Live (Precios Reducidos 50% & Likes)

| Acción YouTube | Monto / Condición | Unidad (`unitType`) | Arma (`weapon`) | Efecto |
| :--- | :--- | :--- | :--- | :--- |
| **👆 10 Likes / Taps** | Chat Likes / Commands | `brawler` | `BAT` | Spawnea matón base con bate de béisbol |
| **💵 SuperChat $1** | $1.00 USD | `swat` | `CARBINERIFLE` | Spawnea SWAT táctico |
| **🚀 SuperChat $2.5** | $2.50 USD | `juggernaut` | `MINIGUN` | Spawnea Juggernaut pesado (600 HP) |
| **🎩 SuperChat $5** | $5.00 USD | `sniper` | `SNIPER` | Spawnea francotirador de élite |
| **💖 SuperChat $10** | $10.00 USD | `ninja` | `KATANA` | Spawnea ninja veloz (300 HP) |
| **🦁 SuperChat $25** | $25.00 USD | `boss` | `RPG` | Spawnea Boss supremo Minion Bob (1000 HP) |
| **🌌 SuperChat $50+** | $50.00+ USD | `container` | `NONE` | Cae contenedor espacial con gran explosión |

