using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using GTA;
using GTA.Math;
using GTA.Native;

namespace TikTokNPCBattles
{
    public class PendingEventData
    {
        public string Team;
        public string UnitType;
        public string Weapon;
        public string Donor;
        public bool Visible;
        public int Limit;
    }

    public class FallingContainer
    {
        public Prop ContainerProp;
        public Vector3 TargetCenter;
        public bool HasExploded;
        public int ExplodeTimestamp;

        public FallingContainer(Prop prop, Vector3 targetCenter)
        {
            ContainerProp = prop;
            TargetCenter = targetCenter;
            HasExploded = false;
            ExplodeTimestamp = 0;
        }
    }

    public class UserTeamInfo
    {
        public string Username;
        public RelationshipGroup RelGroup;
        public BlipColor Color;
        public Color TextColor;
        public List<Ped> ActivePeds;
        public int Kills;

        public UserTeamInfo(string username, RelationshipGroup relGroup, BlipColor color, Color textColor)
        {
            Username = username;
            RelGroup = relGroup;
            Color = color;
            TextColor = textColor;
            ActivePeds = new List<Ped>();
            Kills = 0;
        }
    }

    public class TikTokNPCBattles3 : Script
    {
        private Dictionary<string, UserTeamInfo> userTeams;
        private List<Prop> arenaWalls;
        private List<Prop> firstLevelWalls;
        private List<FallingContainer> activeContainers;
        private bool isInitialized;
        private bool isArenaSet;
        private Vector3 arenaCenter;
        private const float ARENA_SIZE = 12.0f;

        private int maxPedsPerUser = 7;
        private const int MAX_TOTAL_PEDS = 40;

        private bool areFirstLevelWallsVisible = true;

        private Camera orbitCamera;
        private bool isCameraMode;
        private float orbitAngle;
        private const float ORBIT_RADIUS = 12.0f;
        private const float ORBIT_HEIGHT = 4.0f;
        private const float ORBIT_SPEED = 0.046875f;

        private Queue<PendingEventData> incomingEventsQueue;
        private object queueLock = new object();
        private Thread backgroundHttpThread;
        private bool isRunning;
        private HashSet<int> dancingPedHandles = new HashSet<int>();
        private Dictionary<int, WeaponHash> pedAssignedWeapons = new Dictionary<int, WeaponHash>();

        private readonly string[] followerPool = new string[]
        {
            "@Seguidor_1", "@Seguidor_2", "@Fan_GTA5", "@Gamer_Anonimo",
            "@Ultra_Fan", "@Leyenda_Live", "@Seguidor_VIP", "@Brawler_Fan"
        };
        private int followerIndex = 0;
        private int lastAutoSpawnTime = 0;

        private readonly string[] danceScenarios = new string[]
        {
            "WORLD_HUMAN_PARTYING",
            "WORLD_HUMAN_CHEERING",
            "WORLD_HUMAN_JOG_STANDING",
            "WORLD_HUMAN_PARTYING",
            "WORLD_HUMAN_CHEERING"
        };

        private readonly Color[] userColors = new Color[]
        {
            Color.Red, Color.DodgerBlue, Color.LimeGreen, Color.Gold,
            Color.DeepPink, Color.Cyan, Color.Orange, Color.Purple,
            Color.SpringGreen, Color.YellowGreen, Color.Magenta, Color.Coral
        };

        private readonly BlipColor[] blipColors = new BlipColor[]
        {
            BlipColor.Red, BlipColor.Blue, BlipColor.Green, BlipColor.Yellow,
            BlipColor.Pink, BlipColor.Orange, BlipColor.Purple, BlipColor.White
        };

        public TikTokNPCBattles3()
        {
            userTeams = new Dictionary<string, UserTeamInfo>(StringComparer.OrdinalIgnoreCase);
            arenaWalls = new List<Prop>();
            firstLevelWalls = new List<Prop>();
            activeContainers = new List<FallingContainer>();
            incomingEventsQueue = new Queue<PendingEventData>();
            isInitialized = false;
            isArenaSet = false;
            isCameraMode = false;
            orbitAngle = 0.0f;
            arenaCenter = Vector3.Zero;
            isRunning = true;

            Tick += OnTick;
            KeyUp += OnKeyUp;
            Aborted += OnAborted;

            backgroundHttpThread = new Thread(BackgroundHttpWorker);
            backgroundHttpThread.IsBackground = true;
            backgroundHttpThread.Start();
        }

        private void OnAborted(object sender, EventArgs e)
        {
            isRunning = false;
            DisableCameraMode();
            ClearArena();
        }

        private void InitializeScript()
        {
            GTA.UI.Notification.Show("~g~TikTok NPC Battle PRO v14 (Super Explosiones + Boss Alien) Cargado!~w~\n~b~K~w~: Arena 12x12m | ~purple~C~w~: Cámara 360° | ~r~L~w~: Limpiar.");
        }

        private UserTeamInfo GetOrCreateUserTeam(string username)
        {
            string key = username.Trim();
            if (string.IsNullOrEmpty(key)) key = "@Viewer";

            if (!userTeams.ContainsKey(key))
            {
                int index = userTeams.Count;
                Color txtColor = userColors[index % userColors.Length];
                BlipColor bColor = blipColors[index % blipColors.Length];

                RelationshipGroup group = World.AddRelationshipGroup("USER_" + index + "_" + Math.Abs(key.GetHashCode()));

                foreach (UserTeamInfo existingTeam in userTeams.Values)
                {
                    group.SetRelationshipBetweenGroups(existingTeam.RelGroup, Relationship.Hate, true);
                    existingTeam.RelGroup.SetRelationshipBetweenGroups(group, Relationship.Hate, true);
                }

                UserTeamInfo newTeam = new UserTeamInfo(key, group, bColor, txtColor);
                userTeams[key] = newTeam;
            }

            return userTeams[key];
        }

        private void SetArenaPosition()
        {
            Ped p = Game.Player.Character;
            if (p == null || !p.Exists()) return;

            ClearWalls();

            arenaCenter = p.Position + p.ForwardVector * 8.0f;

            SpawnDenseSealedWalls(arenaCenter, ARENA_SIZE);

            isArenaSet = true;
            GTA.UI.Notification.Show("~g~ARENA 12x12m FIJADA!~w~");
        }

        private void SpawnDenseSealedWalls(Vector3 center, float size)
        {
            Model wallModel = new Model("prop_mp_barrier_01");
            wallModel.Request(1000);

            float half = size / 2.0f;
            float[] zOffsets = new float[] { -1.15f, 2.85f, 6.85f, 10.85f, 14.85f, 18.85f, 22.85f, 26.85f };
            float[] sideOffsets = new float[] { -4.5f, -1.5f, 1.5f, 4.5f };

            foreach (float z in zOffsets)
            {
                bool isGroundLevel = (z == -1.15f);

                foreach (float off in sideOffsets)
                {
                    CreateSingleWall(wallModel, center + new Vector3(off, half, z), 0f, isGroundLevel);
                    CreateSingleWall(wallModel, center + new Vector3(off, -half, z), 0f, isGroundLevel);
                    CreateSingleWall(wallModel, center + new Vector3(half, off, z), 90f, isGroundLevel);
                    CreateSingleWall(wallModel, center + new Vector3(-half, off, z), 90f, isGroundLevel);
                }
            }

            foreach (float xOff in sideOffsets)
            {
                foreach (float yOff in sideOffsets)
                {
                    CreateSingleWall(wallModel, center + new Vector3(xOff, yOff, 30.85f), 0f, false);
                }
            }
        }

        private void CreateSingleWall(Model wallModel, Vector3 pos, float rotationZ, bool isGroundLevel)
        {
            Prop wall = World.CreateProp(wallModel, pos, new Vector3(0, 0, rotationZ), false, false);
            if (wall != null && wall.Exists())
            {
                wall.IsPersistent = true;

                bool shouldBeVisible = isGroundLevel && areFirstLevelWallsVisible;
                Function.Call(Hash.SET_ENTITY_VISIBLE, wall.Handle, shouldBeVisible, false);
                Function.Call(Hash.SET_ENTITY_COLLISION, wall.Handle, true, true);
                Function.Call(Hash.FREEZE_ENTITY_POSITION, wall.Handle, true);

                arenaWalls.Add(wall);
                if (isGroundLevel)
                {
                    firstLevelWalls.Add(wall);
                }
            }
        }

        private void ToggleWallsVisibility(bool visible)
        {
            areFirstLevelWallsVisible = visible;
            foreach (Prop w in firstLevelWalls)
            {
                if (w != null && w.Exists())
                {
                    Function.Call(Hash.SET_ENTITY_VISIBLE, w.Handle, areFirstLevelWallsVisible, false);
                }
            }
            GTA.UI.Notification.Show("~y~Barreras del piso: ~w~" + (areFirstLevelWallsVisible ? "~g~VISIBLES" : "~r~INVISIBLES"));
        }

        private void ClearWalls()
        {
            foreach (Prop w in arenaWalls)
            {
                if (w != null && w.Exists()) w.Delete();
            }
            arenaWalls.Clear();
            firstLevelWalls.Clear();
        }

        private void ToggleCameraMode()
        {
            if (!isArenaSet || arenaCenter == Vector3.Zero)
            {
                GTA.UI.Notification.Show("~r~Primero fija la arena con K antes de activar la cámara!~w~");
                return;
            }

            isCameraMode = !isCameraMode;

            if (isCameraMode)
            {
                if (orbitCamera == null)
                {
                    Vector3 initialCamPos = arenaCenter + new Vector3(ORBIT_RADIUS, 0, ORBIT_HEIGHT);
                    orbitCamera = World.CreateCamera(initialCamPos, Vector3.Zero, 50.0f);
                    World.RenderingCamera = orbitCamera;
                }
                GTA.UI.Notification.Show("~purple~🎥 CÁMARA 360° ACTIVADA~w~\nPresiona ~purple~C~w~ para volver al personaje.");
            }
            else
            {
                DisableCameraMode();
                GTA.UI.Notification.Show("~y~🎥 Cámara Desactivada~w~.");
            }
        }

        private void DisableCameraMode()
        {
            isCameraMode = false;
            World.RenderingCamera = null;
            if (orbitCamera != null)
            {
                orbitCamera.Delete();
                orbitCamera = null;
            }
        }

        private void OnKeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (!isInitialized) return;

            if (e.KeyCode == System.Windows.Forms.Keys.C || e.KeyCode == System.Windows.Forms.Keys.F6)
            {
                ToggleCameraMode();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.K || e.KeyCode == System.Windows.Forms.Keys.F7 || e.KeyCode == System.Windows.Forms.Keys.F9)
            {
                SetArenaPosition();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.L || e.KeyCode == System.Windows.Forms.Keys.F10)
            {
                DisableCameraMode();
                ClearArena();
                isArenaSet = false;
                GTA.UI.Notification.Show("~y~Arena Limpiada!~w~ Se eliminaron todos los equipos.");
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (Game.Player == null || Game.Player.Character == null || !Game.Player.Character.Exists()) return;

            if (!isInitialized)
            {
                InitializeScript();
                isInitialized = true;
            }

            Ped playerPed = Game.Player.Character;
            playerPed.IsInvincible = true;
            playerPed.Health = playerPed.MaxHealth;
            playerPed.Armor = 100;
            Function.Call(Hash.SET_ENTITY_PROOFS, playerPed.Handle, true, true, true, true, true, true, true, true);

            if (isArenaSet)
            {
                Function.Call(Hash.SET_CLOCK_TIME, 12, 0, 0);
                Function.Call(Hash.NETWORK_OVERRIDE_CLOCK_TIME, 12, 0, 0);
                World.Weather = Weather.ExtraSunny;
                EnforceSmoothPhysicalContainment();
            }

            ApplyUltraPerformanceOptimizations();
            UpdateOrbitCamera();
            DrawOverheadNamesAndHealthBars();
            UpdateContainersScripted();
            CleanDeadPeds();
            AssignCombatTasksOrVictoryDance();
            ProcessQueuedEvents();
            CheckAutoSpawnFollowers();
        }

        private void EnforceSmoothPhysicalContainment()
        {
            float maxAllowedRadius = (ARENA_SIZE / 2.0f) - 0.3f;

            foreach (UserTeamInfo uTeam in userTeams.Values)
            {
                foreach (Ped ped in uTeam.ActivePeds)
                {
                    if (ped != null && ped.Exists() && ped.IsAlive)
                    {
                        Vector2 center2D = new Vector2(arenaCenter.X, arenaCenter.Y);
                        Vector2 ped2D = new Vector2(ped.Position.X, ped.Position.Y);
                        float dist = Vector2.Distance(center2D, ped2D);

                        if (dist > maxAllowedRadius)
                        {
                            Vector2 dir = (center2D - ped2D);
                            dir.Normalize();
                            float pushSpeed = 4.0f;
                            ped.Velocity = new Vector3(dir.X * pushSpeed, dir.Y * pushSpeed, ped.Velocity.Z);
                        }
                    }
                }
            }
        }

        private void ApplyUltraPerformanceOptimizations()
        {
            if (!isArenaSet || arenaCenter == Vector3.Zero) return;

            Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);
            Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f, 0.0f);
            Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);
            Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);
            Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);
            Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);

            for (int i = 1; i <= 12; i++)
            {
                Function.Call(Hash.ENABLE_DISPATCH_SERVICE, i, false);
            }

            if (Game.Player != null && Game.Player.Character != null)
            {
                Game.Player.WantedLevel = 0;
            }

            Ped[] peds = World.GetNearbyPeds(arenaCenter, 120.0f);
            foreach (Ped p in peds)
            {
                if (p == null || !p.Exists() || p == Game.Player.Character) continue;

                bool isModPed = false;
                foreach (UserTeamInfo uTeam in userTeams.Values)
                {
                    if (uTeam.ActivePeds.Contains(p))
                    {
                        isModPed = true;
                        break;
                    }
                }

                if (!isModPed)
                {
                    p.Delete();
                }
            }

            Vehicle[] vehs = World.GetNearbyVehicles(arenaCenter, 120.0f);
            foreach (Vehicle v in vehs)
            {
                if (v != null && v.Exists())
                {
                    v.Delete();
                }
            }
        }

        private void UpdateOrbitCamera()
        {
            if (!isCameraMode || orbitCamera == null || !isArenaSet || arenaCenter == Vector3.Zero) return;

            orbitAngle += ORBIT_SPEED;
            if (orbitAngle >= 360.0f) orbitAngle -= 360.0f;

            float rad = orbitAngle * (float)Math.PI / 180.0f;
            Vector3 camPos = arenaCenter + new Vector3(
                (float)Math.Cos(rad) * ORBIT_RADIUS,
                (float)Math.Sin(rad) * ORBIT_RADIUS,
                ORBIT_HEIGHT
            );

            orbitCamera.Position = camPos;
            orbitCamera.PointAt(arenaCenter + new Vector3(0, 0, 1.0f));
        }

        private void DrawOverheadNamesAndHealthBars()
        {
            foreach (UserTeamInfo uTeam in userTeams.Values)
            {
                foreach (Ped ped in uTeam.ActivePeds)
                {
                    if (ped != null && ped.Exists() && ped.IsAlive)
                    {
                        DrawOverheadHUD(ped, uTeam.Username, uTeam.TextColor);
                    }
                }
            }
        }

        private void DrawOverheadHUD(Ped ped, string text, Color color)
        {
            Vector3 headPos = ped.Position + new Vector3(0, 0, 1.25f);
            PointF screenPos = GTA.UI.Screen.WorldToScreen(headPos);

            if (screenPos.X > 0 && screenPos.Y > 0 && screenPos.X < GTA.UI.Screen.Width && screenPos.Y < GTA.UI.Screen.Height)
            {
                GTA.UI.TextElement txt = new GTA.UI.TextElement(text, screenPos, 0.64f, color);
                txt.Alignment = GTA.UI.Alignment.Center;
                txt.Outline = true;
                txt.Draw();

                float healthPct = Math.Max(0.0f, Math.Min(1.0f, (float)ped.Health / (float)ped.MaxHealth));
                float barWidth = 60.0f;
                float barHeight = 6.0f;

                PointF bgPos = new PointF(screenPos.X - (barWidth / 2.0f), screenPos.Y + 32.0f);
                SizeF bgSize = new SizeF(barWidth, barHeight);
                GTA.UI.ContainerElement bgBar = new GTA.UI.ContainerElement(bgPos, bgSize, Color.FromArgb(180, 0, 0, 0));
                bgBar.Draw();

                PointF fillPos = new PointF(screenPos.X - (barWidth / 2.0f), screenPos.Y + 32.0f);
                SizeF fillSize = new SizeF(barWidth * healthPct, barHeight);
                Color hpColor = healthPct > 0.5f ? Color.LimeGreen : (healthPct > 0.2f ? Color.Gold : Color.Red);
                GTA.UI.ContainerElement fillBar = new GTA.UI.ContainerElement(fillPos, fillSize, hpColor);
                fillBar.Draw();
            }
        }

        // IMPACTO Y EXPLOSIONES APOCALÍPTICAS MÚLTIPLES DEL CONTENEDOR
        private void UpdateContainersScripted()
        {
            for (int i = activeContainers.Count - 1; i >= 0; i--)
            {
                FallingContainer fc = activeContainers[i];
                if (fc.ContainerProp == null || !fc.ContainerProp.Exists())
                {
                    activeContainers.RemoveAt(i);
                    continue;
                }

                if (!fc.HasExploded)
                {
                    Vector3 currentPos = fc.ContainerProp.Position;
                    fc.ContainerProp.Position = new Vector3(currentPos.X, currentPos.Y, currentPos.Z - 0.65f);
                    fc.ContainerProp.Rotation = new Vector3(90.0f, 0, 0);

                    if (fc.ContainerProp.Position.Z <= arenaCenter.Z + 1.2f)
                    {
                        fc.HasExploded = true;
                        fc.ExplodeTimestamp = Game.GameTime;

                        Vector3 expPos = fc.ContainerProp.Position;

                        // ONDA DE EXPLOSIONES ÉPICAS MÚLTIPLES EN ANILLO EXPANSIVO
                        World.AddExplosion(expPos, ExplosionType.Plane, 15.0f, 10.0f);
                        World.AddExplosion(expPos + new Vector3(2.5f, 2.5f, 0), ExplosionType.TankShell, 12.0f, 8.0f);
                        World.AddExplosion(expPos + new Vector3(-2.5f, 2.5f, 0), ExplosionType.StickyBomb, 12.0f, 8.0f);
                        World.AddExplosion(expPos + new Vector3(2.5f, -2.5f, 0), ExplosionType.GrenadeL, 12.0f, 8.0f);
                        World.AddExplosion(expPos + new Vector3(-2.5f, -2.5f, 0), ExplosionType.Rocket, 12.0f, 8.0f);
                        World.AddExplosion(expPos + new Vector3(4.0f, 0, 0), ExplosionType.Plane, 15.0f, 10.0f);
                        World.AddExplosion(expPos + new Vector3(-4.0f, 0, 0), ExplosionType.Plane, 15.0f, 10.0f);
                        World.AddExplosion(expPos + new Vector3(0, 4.0f, 0), ExplosionType.TankShell, 15.0f, 10.0f);
                        World.AddExplosion(expPos + new Vector3(0, -4.0f, 0), ExplosionType.TankShell, 15.0f, 10.0f);

                        // SACUDIDA CINEMÁTICA DE CÁMARA
                        Function.Call(Hash.SHAKE_GAMEPLAY_CAM, "LARGE_EXPLOSION_SHAKE", 1.5f);

                        GTA.UI.Notification.Show("~r~💥 IMPRACTO METEÓRICO Y EXPLOSIÓN APOCALÍPTICA DEL CONTENEDOR!");
                    }
                }
                else
                {
                    if (Game.GameTime - fc.ExplodeTimestamp >= 3000)
                    {
                        fc.ContainerProp.Delete();
                        activeContainers.RemoveAt(i);
                    }
                }
            }
        }

        private void SpawnContainerDrop(string donorName)
        {
            if (!isArenaSet) return;

            Model containerModel = new Model("prop_container_01a");
            containerModel.Request(500);

            int attempts = 0;
            while (!containerModel.IsLoaded && attempts < 5)
            {
                Script.Wait(10);
                attempts++;
            }

            Vector3 dropPos = arenaCenter + new Vector3(0, 0, 16.0f);

            Prop container = World.CreateProp(containerModel, dropPos, new Vector3(90.0f, 0, 0), false, false);
            if (container != null && container.Exists())
            {
                container.IsPersistent = true;
                container.Rotation = new Vector3(90.0f, 0, 0);
                Function.Call(Hash.FREEZE_ENTITY_POSITION, container.Handle, true);

                activeContainers.Add(new FallingContainer(container, arenaCenter));
                GTA.UI.Notification.Show("~purple~🌌 " + donorName + " LANZÓ UN CONTENEDOR VERTICAL!");
            }
        }

        private void ProcessQueuedEvents()
        {
            int totalActivePeds = 0;
            foreach (UserTeamInfo ut in userTeams.Values) totalActivePeds += ut.ActivePeds.Count;

            if (totalActivePeds >= MAX_TOTAL_PEDS) return;

            PendingEventData evt = null;
            lock (queueLock)
            {
                if (incomingEventsQueue.Count > 0)
                {
                    evt = incomingEventsQueue.Peek();
                }
            }

            if (evt != null)
            {
                if (evt.UnitType == "toggle_walls")
                {
                    lock (queueLock) { incomingEventsQueue.Dequeue(); }
                    ToggleWallsVisibility(evt.Visible);
                }
                else if (evt.UnitType == "set_ped_cap")
                {
                    lock (queueLock) { incomingEventsQueue.Dequeue(); }
                    maxPedsPerUser = Math.Max(1, Math.Min(20, evt.Limit));
                    GTA.UI.Notification.Show("~g~Límite de Peds por cuenta: ~w~" + maxPedsPerUser);
                }
                else if (evt.UnitType == "container")
                {
                    lock (queueLock) { incomingEventsQueue.Dequeue(); }
                    SpawnContainerDrop(evt.Donor);
                }
                else
                {
                    UserTeamInfo userTeam = GetOrCreateUserTeam(evt.Donor);
                    if (userTeam.ActivePeds.Count < maxPedsPerUser)
                    {
                        lock (queueLock) { incomingEventsQueue.Dequeue(); }
                        SpawnNPCForUser(evt.Donor, evt.UnitType, evt.Weapon);
                    }
                    else
                    {
                        lock (queueLock) { incomingEventsQueue.Dequeue(); }
                    }
                }
            }
        }

        private void CheckAutoSpawnFollowers()
        {
            if (!isArenaSet) return;

            List<UserTeamInfo> activeTeamsWithSurvivors = new List<UserTeamInfo>();
            foreach (UserTeamInfo uTeam in userTeams.Values)
            {
                if (uTeam.ActivePeds.Count > 0)
                {
                    activeTeamsWithSurvivors.Add(uTeam);
                }
            }

            if (activeTeamsWithSurvivors.Count <= 1)
            {
                if (Game.GameTime - lastAutoSpawnTime >= 8000)
                {
                    lastAutoSpawnTime = Game.GameTime;

                    string followerName = followerPool[followerIndex % followerPool.Length];
                    followerIndex++;

                    SpawnNPCForUser(followerName, "brawler", "BAT");
                    GTA.UI.Notification.Show("~y~[SEGUIDOR ENTRÓ] ~w~" + followerName + " con Bate!");
                }
            }
        }

        private void BackgroundHttpWorker()
        {
            using (WebClient client = new WebClient())
            {
                while (isRunning)
                {
                    try
                    {
                        string jsonResponse = client.DownloadString("http://localhost:3000/api/pending-events");
                        if (!string.IsNullOrEmpty(jsonResponse) && jsonResponse.Contains("events"))
                        {
                            MatchCollection matches = Regex.Matches(jsonResponse, @"\{[^{}]*""unitType""[^{}]*\}");
                            foreach (Match m in matches)
                            {
                                string str = m.Value;
                                string unitType = ExtractValue(str, "unitType", "standard");
                                string weapon = ExtractValue(str, "weapon", "PISTOL");
                                string donor = ExtractValue(str, "donor", "Viewer");

                                bool visible = str.Contains("\"visible\":true");
                                int limit = 6;
                                Match limitMatch = Regex.Match(str, @"""limit"":\s*(\d+)");
                                if (limitMatch.Success) int.TryParse(limitMatch.Groups[1].Value, out limit);

                                lock (queueLock)
                                {
                                    if (incomingEventsQueue.Count < 30)
                                    {
                                        incomingEventsQueue.Enqueue(new PendingEventData
                                        {
                                            Team = donor,
                                            UnitType = unitType,
                                            Weapon = weapon,
                                            Donor = donor,
                                            Visible = visible,
                                            Limit = limit
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    Thread.Sleep(500);
                }
            }
        }

        private string ExtractValue(string jsonStr, string key, string fallback)
        {
            Match m = Regex.Match(jsonStr, "\"" + key + "\":\\s*\"([^\"]+)\"");
            if (m.Success) return m.Groups[1].Value;
            return fallback;
        }

        private void SpawnNPCForUser(string donorName, string unitType, string weaponName)
        {
            if (!isArenaSet) return;

            UserTeamInfo userTeam = GetOrCreateUserTeam(donorName);

            float offset = (ARENA_SIZE / 2.0f) - 1.0f;
            Vector3 spawnLoc = arenaCenter + new Vector3(
                (float)(new Random().NextDouble() * (offset * 2) - offset),
                (float)(new Random().NextDouble() * (offset * 2) - offset),
                0
            );

            string modelName = "g_m_y_ballaeast_01";
            if (unitType == "brawler") modelName = "g_m_y_salvaboss_01";
            else if (unitType == "swat") modelName = "s_m_y_swat_01";
            else if (unitType == "alien") modelName = "u_m_y_zombie_01";
            else if (unitType == "juggernaut") modelName = "s_m_m_ciasec_01";
            else if (unitType == "sniper") modelName = "s_m_y_sheriff_01";
            else if (unitType == "ninja") modelName = "g_m_y_korean_01";
            else if (unitType == "boss") modelName = "s_m_m_movalien_01";

            Model model = new Model(modelName);
            model.Request(500);

            int attempts = 0;
            while (!model.IsLoaded && attempts < 5)
            {
                Script.Wait(10);
                attempts++;
            }

            if (!model.IsLoaded)
            {
                modelName = "g_m_y_ballaeast_01";
                model = new Model(modelName);
                model.Request(500);
                Script.Wait(10);
            }

            Ped ped = World.CreatePed(model, spawnLoc);
            if (ped == null) return;

            ped.IsPersistent = true;
            ped.RelationshipGroup = userTeam.RelGroup;

            WeaponHash wHash = WeaponHash.Pistol;
            if (weaponName == "BAT" || unitType == "brawler") wHash = WeaponHash.Bat;
            else if (weaponName == "MICROSMG") wHash = WeaponHash.MicroSMG;
            else if (weaponName == "CARBINERIFLE") wHash = WeaponHash.CarbineRifle;
            else if (weaponName == "RAYPISTOL") wHash = WeaponHash.APPistol;
            else if (weaponName == "MINIGUN") wHash = WeaponHash.Minigun;
            else if (weaponName == "SNIPER") wHash = WeaponHash.SniperRifle;
            else if (weaponName == "KATANA") wHash = WeaponHash.Machete;
            else if (weaponName == "RPG") wHash = WeaponHash.RPG;

            ped.Weapons.Give(wHash, 999, true, true);
            pedAssignedWeapons[ped.Handle] = wHash;

            if (unitType == "brawler")
            {
                ped.MaxHealth = 220;
                ped.Health = 220;
                ped.Armor = 50;
            }
            else if (unitType == "juggernaut")
            {
                ped.MaxHealth = 600;
                ped.Health = 600;
                ped.Armor = 200;
            }
            else if (unitType == "boss") // BOSS SUPREMO "BOB"
            {
                ped.MaxHealth = 1000;
                ped.Health = 1000;
                ped.Armor = 300;
            }
            else if (unitType == "ninja")
            {
                ped.MaxHealth = 300;
                ped.Health = 300;
                ped.Armor = 100;
            }
            else
            {
                ped.MaxHealth = 160;
                ped.Health = 160;
                ped.Armor = 50;
            }

            ped.Accuracy = 85;
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 46, true);
            Function.Call(Hash.SET_PED_COMBAT_ABILITY, ped.Handle, 2);

            Blip blip = ped.AddBlip();
            blip.Color = userTeam.Color;
            blip.Scale = 0.6f;

            userTeam.ActivePeds.Add(ped);

            GTA.UI.Notification.Show("~g~+" + unitType.ToUpper() + "~w~ (" + donorName + ")");

            TargetNearestEnemy(ped, userTeam);
        }

        private void TargetNearestEnemy(Ped ped, UserTeamInfo currentTeam)
        {
            Ped closestEnemy = null;
            float minDistance = 9999f;

            foreach (UserTeamInfo otherTeam in userTeams.Values)
            {
                if (otherTeam == currentTeam) continue;

                foreach (Ped enemyPed in otherTeam.ActivePeds)
                {
                    if (enemyPed != null && enemyPed.Exists() && enemyPed.IsAlive)
                    {
                        float dist = ped.Position.DistanceTo(enemyPed.Position);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            closestEnemy = enemyPed;
                        }
                    }
                }
            }

            if (closestEnemy != null)
            {
                Function.Call(Hash.TASK_COMBAT_PED, ped.Handle, closestEnemy.Handle, 0, 16);
            }
        }

        private void AssignCombatTasksOrVictoryDance()
        {
            List<UserTeamInfo> activeTeamsWithSurvivors = new List<UserTeamInfo>();

            foreach (UserTeamInfo uTeam in userTeams.Values)
            {
                if (uTeam.ActivePeds.Count > 0)
                {
                    activeTeamsWithSurvivors.Add(uTeam);
                }
            }

            if (activeTeamsWithSurvivors.Count == 1)
            {
                UserTeamInfo winnerTeam = activeTeamsWithSurvivors[0];
                for (int i = 0; i < winnerTeam.ActivePeds.Count; i++)
                {
                    Ped survivor = winnerTeam.ActivePeds[i];
                    if (survivor != null && survivor.Exists() && survivor.IsAlive)
                    {
                        if (!dancingPedHandles.Contains(survivor.Handle))
                        {
                            dancingPedHandles.Add(survivor.Handle);
                            survivor.Task.ClearAllImmediately();
                            Function.Call(Hash.SET_CURRENT_PED_WEAPON, survivor.Handle, (uint)WeaponHash.Unarmed, true);
                            Function.Call(Hash.SET_PED_CURRENT_WEAPON_VISIBLE, survivor.Handle, false, true, true, true);

                            string scenario = (i % 2 == 0) ? "WORLD_HUMAN_PARTYING" : "WORLD_HUMAN_CHEERING";
                            Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, survivor.Handle, scenario, 0, true);
                        }
                    }
                }
            }
            else
            {
                if (dancingPedHandles.Count > 0)
                {
                    foreach (UserTeamInfo uTeam in userTeams.Values)
                    {
                        foreach (Ped ped in uTeam.ActivePeds)
                        {
                            if (ped != null && ped.Exists() && ped.IsAlive && dancingPedHandles.Contains(ped.Handle))
                            {
                                ped.Task.ClearAllImmediately();
                                Function.Call(Hash.SET_PED_CURRENT_WEAPON_VISIBLE, ped.Handle, true, true, true, true);
                                if (pedAssignedWeapons.ContainsKey(ped.Handle))
                                {
                                    ped.Weapons.Select(pedAssignedWeapons[ped.Handle], true);
                                }
                            }
                        }
                    }
                    dancingPedHandles.Clear();
                }

                foreach (UserTeamInfo uTeam in userTeams.Values)
                {
                    foreach (Ped ped in uTeam.ActivePeds)
                    {
                        if (ped != null && ped.IsAlive && !ped.IsInCombat)
                        {
                            if (pedAssignedWeapons.ContainsKey(ped.Handle))
                            {
                                ped.Weapons.Select(pedAssignedWeapons[ped.Handle], true);
                            }
                            TargetNearestEnemy(ped, uTeam);
                        }
                    }
                }
            }
        }

        private void CleanDeadPeds()
        {
            foreach (UserTeamInfo uTeam in userTeams.Values)
            {
                uTeam.ActivePeds.RemoveAll(delegate(Ped p) {
                    return p == null || !p.Exists() || p.IsDead;
                });
            }
        }

        private void ClearArena()
        {
            ClearWalls();

            foreach (FallingContainer fc in activeContainers)
            {
                if (fc.ContainerProp != null && fc.ContainerProp.Exists()) fc.ContainerProp.Delete();
            }
            activeContainers.Clear();

            foreach (UserTeamInfo uTeam in userTeams.Values)
            {
                foreach (Ped p in uTeam.ActivePeds)
                {
                    if (p != null && p.Exists()) p.Delete();
                }
                uTeam.ActivePeds.Clear();
            }
            dancingPedHandles.Clear();
            pedAssignedWeapons.Clear();
            userTeams.Clear();
        }
    }
}
