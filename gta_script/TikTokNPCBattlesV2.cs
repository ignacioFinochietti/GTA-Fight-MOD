using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Text.RegularExpressions;
using GTA;
using GTA.Math;
using GTA.Native;

namespace TikTokNPCBattles
{
    public class TeamInfo
    {
        public string Name;
        public int RelGroup;
        public Vector3 SpawnPos;
        public BlipColor Color;
        public Color MarkerColor;
        public string DefaultModelName;
        public List<Ped> ActivePeds;

        public TeamInfo(string name, BlipColor color, Color markerColor, string defaultModelName)
        {
            Name = name;
            Color = color;
            MarkerColor = markerColor;
            DefaultModelName = defaultModelName;
            ActivePeds = new List<Ped>();
            SpawnPos = Vector3.Zero;
        }
    }

    public class TikTokNPCBattlesV2 : Script
    {
        private Dictionary<string, TeamInfo> teams;
        private int lastPollTime;
        private bool isInitialized;
        private bool isArenaSet;
        private Vector3 arenaCenter;
        private const float ARENA_SIZE = 5.0f;
        private const int POLL_INTERVAL_MS = 500;

        public TikTokNPCBattlesV2()
        {
            teams = new Dictionary<string, TeamInfo>();
            lastPollTime = 0;
            isInitialized = false;
            isArenaSet = false;
            arenaCenter = Vector3.Zero;

            Tick += OnTick;
            KeyUp += OnKeyUp;
        }

        public static int JenkinsHash(string input)
        {
            if (string.IsNullOrEmpty(input)) return 0;
            uint hash = 0;
            string lower = input.ToLower();
            for (int i = 0; i < lower.Length; i++)
            {
                hash += (uint)lower[i];
                hash += (hash << 10);
                hash ^= (hash >> 6);
            }
            hash += (hash << 3);
            hash ^= (hash >> 11);
            hash += (hash << 15);
            return (int)hash;
        }

        private void InitializeTeams()
        {
            teams["red"] = new TeamInfo("ROJO", BlipColor.Red, Color.Red, "g_m_y_ballaeast_01");
            teams["blue"] = new TeamInfo("AZUL", BlipColor.Blue, Color.DodgerBlue, "g_m_y_famca_01");
            teams["green"] = new TeamInfo("VERDE", BlipColor.Green, Color.LimeGreen, "g_m_y_vagos01");
            teams["yellow"] = new TeamInfo("AMARILLO", BlipColor.Yellow, Color.Gold, "g_m_y_mexgoon_01");

            foreach (KeyValuePair<string, TeamInfo> kvp in teams)
            {
                int groupHash = JenkinsHash("TEAM_" + kvp.Key.ToUpper());
                Function.Call(Hash.ADD_RELATIONSHIP_GROUP, "TEAM_" + kvp.Key.ToUpper(), groupHash);
                kvp.Value.RelGroup = groupHash;
            }

            foreach (TeamInfo t1 in teams.Values)
            {
                foreach (TeamInfo t2 in teams.Values)
                {
                    if (t1 != t2)
                    {
                        Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 5, t1.RelGroup, t2.RelGroup);
                        Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 5, t2.RelGroup, t1.RelGroup);
                    }
                }
            }

            ShowSubtitle("~g~TikTok NPC Battle V2 Listo!~w~ Presiona ~b~K~w~ para mover arena | ~r~L~w~ (Limpiar)");
        }

        private bool SetArenaPosition()
        {
            if (Game.Player == null || Game.Player.Character == null || !Game.Player.Character.Exists()) return false;

            Ped p = Game.Player.Character;
            arenaCenter = p.Position + p.ForwardVector * 6.0f;
            float offset = ARENA_SIZE / 2.0f;

            teams["red"].SpawnPos = arenaCenter + new Vector3(-offset, -offset, 0);
            teams["blue"].SpawnPos = arenaCenter + new Vector3(offset, offset, 0);
            teams["green"].SpawnPos = arenaCenter + new Vector3(-offset, offset, 0);
            teams["yellow"].SpawnPos = arenaCenter + new Vector3(offset, -offset, 0);

            foreach (TeamInfo team in teams.Values)
            {
                Blip b = World.CreateBlip(team.SpawnPos);
                b.Color = team.Color;
                b.Name = "Spawn " + team.Name;
                b.Scale = 0.7f;
            }

            isArenaSet = true;
            ShowSubtitle("~g~Arena 5x5m Activada!~w~ Tecla K: Mover | L: Limpiar");
            return true;
        }

        private void OnKeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (!isInitialized) return;

            if (e.KeyCode == System.Windows.Forms.Keys.K || e.KeyCode == System.Windows.Forms.Keys.F7 || e.KeyCode == System.Windows.Forms.Keys.F9)
            {
                SetArenaPosition();
            }
            else if (e.KeyCode == System.Windows.Forms.Keys.L || e.KeyCode == System.Windows.Forms.Keys.F10)
            {
                ClearArena();
                isArenaSet = false;
                ShowSubtitle("~y~Arena Limpiada!~w~ Se eliminaron todos los luchadores.");
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (Game.Player == null || Game.Player.Character == null || !Game.Player.Character.Exists()) return;

            if (!isInitialized)
            {
                InitializeTeams();
                isInitialized = true;
            }

            // Auto-activar arena cuando el personaje esté listo en el mundo
            if (!isArenaSet)
            {
                SetArenaPosition();
            }

            // Dibujar perimetro 3D de 5x5m en el suelo
            if (isArenaSet && arenaCenter != Vector3.Zero)
            {
                World.DrawMarker(
                    MarkerType.DebugSphere,
                    arenaCenter,
                    Vector3.Zero,
                    Vector3.Zero,
                    new Vector3(0.5f, 0.5f, 0.5f),
                    Color.White
                );

                foreach (TeamInfo team in teams.Values)
                {
                    if (team.SpawnPos != Vector3.Zero)
                    {
                        World.DrawMarker(
                            MarkerType.VerticalCylinder,
                            team.SpawnPos,
                            Vector3.Zero,
                            Vector3.Zero,
                            new Vector3(1.5f, 1.5f, 1.8f),
                            team.MarkerColor
                        );
                    }
                }
            }

            CleanDeadPeds();
            AssignCombatTasks();

            if (Game.GameTime - lastPollTime > POLL_INTERVAL_MS)
            {
                lastPollTime = Game.GameTime;
                FetchPendingEvents();
            }
        }

        private void FetchPendingEvents()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    string jsonResponse = client.DownloadString("http://localhost:3000/api/pending-events");
                    if (string.IsNullOrEmpty(jsonResponse) || !jsonResponse.Contains("events")) return;

                    MatchCollection matches = Regex.Matches(jsonResponse, @"\{[^{}]*""team""[^{}]*\}");
                    foreach (Match m in matches)
                    {
                        string str = m.Value;
                        string team = ExtractValue(str, "team", "red");
                        string unitType = ExtractValue(str, "unitType", "standard");
                        string weapon = ExtractValue(str, "weapon", "PISTOL");
                        string donor = ExtractValue(str, "donor", "Fan");

                        if (!teams.ContainsKey(team.ToLower())) team = "red";

                        SpawnNPC(team.ToLower(), unitType, weapon, donor);
                    }
                }
            }
            catch { }
        }

        private string ExtractValue(string jsonStr, string key, string fallback)
        {
            Match m = Regex.Match(jsonStr, "\"" + key + "\":\\s*\"([^\"]+)\"");
            if (m.Success) return m.Groups[1].Value;
            return fallback;
        }

        private void SpawnNPC(string teamKey, string unitType, string weaponName, string donorName)
        {
            // Auto-activar arena si aún no está lista al recibir regalo
            if (!isArenaSet)
            {
                if (!SetArenaPosition()) return;
            }

            TeamInfo team = teams[teamKey];
            Vector3 spawnLoc = team.SpawnPos + new Vector3((float)(new Random().NextDouble() * 2.0 - 1.0), (float)(new Random().NextDouble() * 2.0 - 1.0), 0);

            string modelName = team.DefaultModelName;
            if (unitType == "swat") modelName = "s_m_y_swat_01";
            else if (unitType == "alien") modelName = "u_m_y_zombie_01";
            else if (unitType == "juggernaut") modelName = "s_m_m_ciasec_01";
            else if (unitType == "boss") modelName = "s_m_m_movalien_01";

            Model model = new Model(modelName);
            model.Request(2000);

            int attempts = 0;
            while (!model.IsLoaded && attempts < 20)
            {
                Script.Wait(50);
                attempts++;
            }

            if (!model.IsLoaded)
            {
                model = new Model(PedHash.FreemodeMale01);
                model.Request(1000);
                Script.Wait(50);
            }

            Ped ped = World.CreatePed(model, spawnLoc);
            if (ped == null) return;

            Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, ped.Handle, true, true);
            Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, ped.Handle, team.RelGroup);

            WeaponHash wHash = WeaponHash.Pistol;
            if (weaponName == "MICROSMG") wHash = WeaponHash.MicroSMG;
            else if (weaponName == "CARBINERIFLE") wHash = WeaponHash.CarbineRifle;
            else if (weaponName == "RAYPISTOL") wHash = WeaponHash.APPistol;
            else if (weaponName == "MINIGUN") wHash = WeaponHash.Minigun;
            else if (weaponName == "RPG") wHash = WeaponHash.RPG;

            ped.Weapons.Give(wHash, 999, true, true);

            if (unitType == "juggernaut" || unitType == "boss")
            {
                ped.MaxHealth = 600;
                ped.Health = 600;
                ped.Armor = 200;
            }
            else
            {
                ped.MaxHealth = 160;
                ped.Health = 160;
                ped.Armor = 50;
            }

            ped.Accuracy = 80;
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 46, true);
            Function.Call(Hash.SET_PED_COMBAT_ABILITY, ped.Handle, 2);

            Blip blip = ped.AddBlip();
            blip.Color = team.Color;
            blip.Scale = 0.6f;

            team.ActivePeds.Add(ped);

            ShowSubtitle("~g~+1 " + team.Name + "~w~ (" + donorName + ")");

            TargetNearestEnemy(ped, teamKey);
        }

        private void TargetNearestEnemy(Ped ped, string currentTeamKey)
        {
            Ped closestEnemy = null;
            float minDistance = 9999f;

            foreach (KeyValuePair<string, TeamInfo> kvp in teams)
            {
                if (kvp.Key == currentTeamKey) continue;

                foreach (Ped enemyPed in kvp.Value.ActivePeds)
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

        private void AssignCombatTasks()
        {
            foreach (KeyValuePair<string, TeamInfo> kvp in teams)
            {
                foreach (Ped ped in kvp.Value.ActivePeds)
                {
                    if (ped.IsAlive && !ped.IsInCombat)
                    {
                        TargetNearestEnemy(ped, kvp.Key);
                    }
                }
            }
        }

        private void CleanDeadPeds()
        {
            foreach (TeamInfo team in teams.Values)
            {
                team.ActivePeds.RemoveAll(delegate(Ped p) { return p == null || !p.Exists() || p.IsDead; });
            }
        }

        private void ClearArena()
        {
            foreach (TeamInfo team in teams.Values)
            {
                foreach (Ped p in team.ActivePeds)
                {
                    if (p != null && p.Exists()) p.Delete();
                }
                team.ActivePeds.Clear();
            }
        }

        private void ShowSubtitle(string message)
        {
            Function.Call(Hash._SET_NOTIFICATION_TEXT_ENTRY, "STRING");
            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, message);
            Function.Call(Hash._DRAW_NOTIFICATION, false, true);
        }
    }
}
