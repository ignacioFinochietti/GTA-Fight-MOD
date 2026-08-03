# GTA V TikTok Live NPC Battles (Stream to Earn System) 🎮🔥

Sistema interactivo open-source para transmisiones en vivo. Convierte regalos y me gustas (taps) del chat de **TikTok Live** (y **YouTube Live**) en batallas automáticas de NPCs y eventos dentro de **GTA V (Story Mode / Legacy Edition)**.

---

## 🌟 Características Principales

1. **Conexión Directa a TikTok Live**:
   - Integración nativa con `tiktok-live-connector` sin necesidad de suscripciones o servicios de terceros.
   - Detección en tiempo real de regalos (`rose`, `ice cream`, `donut`, `rocket`, `hat`, `heart`, `lion`, `universe`) y me gustas (`taps`).

2. **Batallas de NPCs por Equipos**:
   - Cada donador crea su propio equipo de NPCs en la arena de GTA V.
   - Asignación inteligente de IA de combate (`Task.Combat`), animaciones universales de baile/celebración al ganar, e interacciones dinámicas.

3. **Overlay Ultra-Moderno para OBS / TikTok Studio**:
   - Diseño visual dark mode con glassmorphism.
   - Marcadores VS en vivo, barras de salud overhead, tabla de posiciones (*Top Donadores*) y feed animado de regalos.
   - Vistas optimizadas para horizontal (16:9) y TikTok vertical (9:16).

4. **Script C# Optimizado para GTA V**:
   - Ejecutable en GTA V Story Mode con `ScriptHookVDotNet v3`.
   - Límite dinámico de 7 personajes vivos simultáneos por cuenta para preservar rendimiento (60 FPS).
   - Auto-spawn configurable para mantener actividad en la arena cuando está vacía.

---

## 📐 Estructura del Proyecto

```
Stream to earn/
├── server/                    # Servidor local Node.js (conector TikTok Live + WebSockets + REST API)
│   ├── package.json
│   └── index.js
├── gta_script/                # Script en C# para GTA V (ScriptHookVDotNet)
│   └── TikTokNPCBattles3.cs
├── overlay/                   # Interfaz visual para OBS Studio / TikTok Studio
│   ├── index.html
│   ├── admin.html             # Panel de Control & Tester
│   ├── styles.css
│   ├── app.js
│   ├── tiktok_live.html       # Formato Vertical 9:16
│   └── youtube_live.html
├── iniciar_servidor.bat       # Launcher en batch para Windows
└── README.md
```

---

## 🛠️ Requisitos e Instalación

### 1. Requisitos para GTA V
- **GTA V (Story Mode)** (Desactivar BattlEye en el launcher de Rockstar/Steam con `-nobattleye`).
- **ScriptHookV**: Copiar `ScriptHookV.dll` y `dinput8.dll` a la carpeta raíz del juego.
- **ScriptHookVDotNet v3.6.0+**: Copiar los binarios a la carpeta raíz del juego.
- **Carpeta `scripts`**: Crear la carpeta `scripts` en la raíz de GTA V (ej: `C:\Program Files\Rockstar Games\Grand Theft Auto V\scripts`).

### 2. Configurar el Script en GTA V
1. Copia `gta_script/TikTokNPCBattles3.cs` a la carpeta `scripts/` de tu GTA V.
2. Presiona `Insert` dentro del juego para recargar los scripts.

### 3. Configurar e Iniciar el Servidor
1. Abre la terminal en la carpeta `server/`:
   ```bash
   cd server
   npm install
   ```
2. Ejecuta `iniciar_servidor.bat` o inicia manualmente con:
   ```bash
   npm start
   ```
3. Ingresa al Panel de Control en `http://localhost:3000/admin.html` para colocar tu usuario de TikTok.

### 4. Configurar el Overlay en OBS Studio
1. Añade una fuente de tipo **Navegador (Browser Source)** en OBS.
2. Configura la URL en `http://localhost:3000/index.html` o selecciona el archivo local.
3. Establece la resolución en `1920` x `1080` (o `1080` x `1920` para vertical).

---

## 🎁 Mapeo de Regalos y Acciones

| Regalo / Acción | Unidad | Arma | Vida / Armadura | Efecto en GTA V |
| :--- | :--- | :--- | :--- | :--- |
| **🌹 Rosa** | Pandillero | MicroSMG | 160 HP / 50 Armor | Tropa ligera |
| **🍦 Helado** | SWAT | Carabinero | 160 HP / 50 Armor | Tropa táctica |
| **🍩 Dona** | Alien | Pistola Rayo | 160 HP / 50 Armor | Tropa especial |
| **🚀 Cohete** | Juggernaut | Minigun | 600 HP / 200 Armor | Tanque pesado |
| **🎩 Sombrero** | Francotirador | Sniper | 160 HP / 50 Armor | Ataque a distancia |
| **💖 Corazón** | Ninja | Machete / Katana | 300 HP / 100 Armor | Velocidad melé |
| **🦁 León** | Boss Alien | RPG | 1000 HP / 300 Armor | Boss supremo |
| **🌌 Universo** | Contenedor | N/A | Explosión múltiple | Caída espacial meteórica |
| **👆 2 Taps / Likes** | Bateador | Bate de béisbol | 220 HP / 50 Armor | Tropa básica por likes |

---

## 🎮 Controles en GTA V

- **`K`** (o **`F7`** / **`F9`**): Fijar arena de combate (12x12m) frente al personaje.
- **`C`** (o **`F6`**): Activar / Desactivar Cámara Orbital 360°.
- **`L`** (o **`F10`**): Limpiar arena y eliminar todos los equipos.

---

## 📄 Licencia

Este proyecto es open source bajo la licencia MIT.
