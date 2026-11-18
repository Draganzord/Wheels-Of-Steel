using UnityEngine;
using UnityEngine.InputSystem;

namespace FourCorners
{
    public sealed class YardHud : MonoBehaviour
    {
        [SerializeField] private SalvageEncounter encounter;
        [SerializeField] private TruckDriver truckDriver;
        [SerializeField] private Camera view;

        private static readonly Color Panel = new(0.09f, 0.078f, 0.068f, 0.95f);
        private static readonly Color Cream = new(1f, 0.92f, 0.77f);
        private static readonly Color Dust = new(0.78f, 0.72f, 0.61f);
        private static readonly Color Gold = new(1f, 0.67f, 0.27f);
        private static readonly Color Brick = new(0.91f, 0.32f, 0.20f);
        private static readonly Color Blue = new(0.35f, 0.65f, 0.96f);
        private static readonly Color Green = new(0.59f, 0.77f, 0.31f);
        private static readonly Color Empty = new(0.31f, 0.27f, 0.22f);
        private static readonly Color Rivet = new(1f, 0.68f, 0.25f);
        private static readonly Color Jackal = new(0.66f, 0.82f, 0.39f);

        private GUIStyle title;
        private GUIStyle label;
        private GUIStyle fine;
        private GUIStyle number;
        private YardRouteMap routeMap;

        public void Configure(SalvageEncounter session, TruckDriver opponent, Camera camera)
        {
            encounter = session;
            truckDriver = opponent;
            view = camera;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            title ??= Style(26, FontStyle.Bold);
            label ??= Style(17, FontStyle.Bold);
            fine ??= Style(15, FontStyle.Normal);
            number ??= Style(32, FontStyle.Bold);
            routeMap ??= new YardRouteMap(encounter.Arena, view);

            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 810f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));

            Card(22, 21, 293, 66, Gold);
            Text(37, 27, "FOUR CORNERS", title, Cream);
            Text(38, 58, "SALVAGE YARD", label, Gold);

            float opponentX = width - 265;
            TruckIntent intent = truckDriver.Intent;
            Color statusColor = intent == TruckIntent.LiningUp ? Gold : intent == TruckIntent.Wrecked ? Dust : Brick;
            int extraOpponents = encounter.Opponents.Count - 1;
            Card(opponentX, 21, 243, 66 + extraOpponents * 27, statusColor);
            Text(opponentX + 14, 29, "WRECKER", label, Cream);
            Text(opponentX + 118, 30, IntentLabel(intent), fine, statusColor);
            Bar(opponentX + 15, 59, 212, 13, encounter.Truck.ArmorFraction, Brick, 10);
            for (int index = 1; index < encounter.Opponents.Count; index++)
            {
                CombatVehicle rival = encounter.Opponents[index];
                float row = 76 + (index - 1) * 27;
                Text(opponentX + 14, row, rival.GetComponent<RivalDriver>().Callsign, fine, rival.Destroyed ? Dust : OpponentColor(index));
                Bar(opponentX + 115, row + 5, 112, 9, rival.ArmorFraction, OpponentColor(index), 8);
            }
            DrawMap(opponentX, 96 + extraOpponents * 27);

            float bottom = height - 98;
            Card(22, bottom, 327, 76, Blue);
            int kmh = Mathf.RoundToInt(encounter.Player.Vehicle.Speed * 3.6f);
            Text(36, bottom + 7, kmh.ToString("00"), number, Cream);
            Text(37, bottom + 47, "KM/H", fine, Dust);
            Text(123, bottom + 8, "MUSCLE  /  ARMOR", label, Cream);
            Bar(124, bottom + 34, 208, 13, encounter.Player.ArmorFraction, Green, 10);
            DrawSurfaceReadout(124, bottom + 53);

            float weaponX = width - 420;
            Card(weaponX, bottom, 124, 76, Gold);
            Text(weaponX + 12, bottom + 8, "GUN", label, Cream);
            Text(weaponX + 12, bottom + 45, "LMB   /   ∞", fine, Gold);
            Card(weaponX + 135, bottom, 128, 76, Gold);
            Text(weaponX + 147, bottom + 8, "MINES", label, Cream);
            Text(weaponX + 147, bottom + 45, $"RMB   /   {encounter.Mines}", fine, Gold);
            Card(weaponX + 274, bottom, 124, 76, Brick);
            Text(weaponX + 286, bottom + 8, "ROCKETS", label, Cream);
            Text(weaponX + 286, bottom + 45, $"E   /   {encounter.Rockets}", fine, Gold);

            DrawWreckerMarker(scale, height, intent);
            DrawStatus(width, height, intent);
            DrawRocketAim(scale, width, height);
            GUI.matrix = previous;
        }

        private void DrawMap(float left, float top)
        {
            bool open = encounter.Arena.Fence.IsBroken;
            Color gate = open ? Green : Gold;
            Card(left, top, 243, 153, YardRouteMap.Creek);
            Legend(left + 14, top + 32, "Asphalt", YardRouteMap.Asphalt);
            Legend(left + 14, top + 59, "Creek cut", YardRouteMap.Creek);
            Legend(left + 14, top + 86, open ? "Gate open" : "Gate shut", gate);
            Legend(left + 14, top + 113, "Ramps", YardRouteMap.Ramp);
            routeMap.Draw(new Rect(left + 108, top + 16, 116, 120), encounter.Player.transform,
                encounter.Opponents, Blue, Dust, gate);
        }

        internal static Color OpponentColor(int index) => index switch
        {
            1 => Rivet,
            2 => Jackal,
            _ => Brick
        };

        private void DrawRocketAim(float scale, float width, float height)
        {
            if (Mouse.current == null || encounter.Rockets == 0 || encounter.Player.Destroyed)
                return;

            Vector2 pointer = Mouse.current.position.ReadValue() / scale;
            float y = height - pointer.y;
            if (pointer.x < 10 || pointer.x > width - 10 || y < 90 || y > height - 105)
                return;

            Fill(new Rect(pointer.x - 9, y - 1, 5, 2), Gold);
            Fill(new Rect(pointer.x + 4, y - 1, 5, 2), Gold);
            Fill(new Rect(pointer.x - 1, y - 9, 2, 5), Gold);
            Fill(new Rect(pointer.x - 1, y + 4, 2, 5), Gold);
        }

        private void Legend(float x, float y, string caption, Color color)
        {
            Fill(new Rect(x, y + 6, 9, 3), color);
            Text(x + 15, y, caption, fine, Dust);
        }

        private void DrawSurfaceReadout(float x, float y)
        {
            ArcadeVehicle vehicle = encounter.Player.Vehicle;
            WheelContact[] wheels = vehicle.Contacts;
            SurfaceKind? first = null;
            SurfaceKind? second = null;
            bool varied = false;
            for (int index = 0; index < wheels.Length; index++)
            {
                SurfaceKind? kind = wheels[index].Grounded && wheels[index].Surface != null
                    ? wheels[index].Surface.Kind : null;
                Fill(new Rect(x + (index & 1) * 11, y + index / 2 * 8, 8, 5), SurfaceColor(kind));
                if (kind == null)
                    continue;
                first ??= kind;
                if (kind != first)
                {
                    second ??= kind;
                    varied |= kind != second;
                }
            }
            string readout = encounter.Player.Destroyed ? "WRECKED"
                : vehicle.OverturnedTime > 0 ? "SELF-RIGHTING"
                : vehicle.IsAirborne ? "AIRBORNE"
                : varied ? "MIXED SURFACES"
                : second.HasValue ? $"{SurfaceLabel(first)} / {SurfaceLabel(second)}" : SurfaceLabel(first);
            Text(x + 30, y - 2, readout, fine, Dust);
        }

        private void DrawStatus(float width, float height, TruckIntent intent)
        {
            float middle = width * 0.5f;
            if (encounter.Result != EncounterResult.Active)
            {
                bool won = encounter.Result == EncounterResult.Won;
                Card(middle - 214, 21, 428, 84, won ? Gold : Brick);
                Center(middle, 29, won ? "YARD CLEARED" : "YOU'RE WRECKED", title, Cream);
                Center(middle, 69, won ? "Explore the yard   ·   R Restart" : "R Restart encounter", label, Dust);
                return;
            }

            bool broken = encounter.Arena.Fence.IsBroken;
            Card(middle - 218, 22, 436, 35, broken ? Green : Gold);
            Center(middle, 30, $"{encounter.OpponentsRemaining} HOSTILE{(encounter.OpponentsRemaining == 1 ? "" : "S")}  /  {(broken ? "GATE OPEN" : "GATE SHUT")}", label, Cream);

            float bottom = height - 83;
            Fill(new Rect(middle - 226, bottom, 452, 56), Panel);
            string routeHint = intent switch
            {
                TruckIntent.Patrolling => "Get close to draw out the wrecker",
                TruckIntent.LiningUp => "Ram coming. Get ready to turn out.",
                TruckIntent.Ramming => "Dodge sideways while the truck is committed",
                _ => broken ? "Use the open shortcut to lose your pursuers" : "Cross the creek or blast open the bank gate"
            };
            Center(middle, bottom + 8, routeHint, label, Gold);
            Center(middle, bottom + 33, "WASD Drive   ·   SPACE Handbrake   ·   E Rocket at cursor", fine, Dust);
        }

        private void DrawWreckerMarker(float scale, float height, TruckIntent intent)
        {
            if (encounter.Truck.Destroyed)
                return;

            Vector3 point = view.WorldToScreenPoint(encounter.Truck.transform.position + Vector3.up * 4.5f);
            if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height)
                return;

            float x = point.x / scale;
            float y = height - point.y / scale;
            if (intent == TruckIntent.LiningUp || intent == TruckIntent.Ramming)
            {
                bool rushing = intent == TruckIntent.Ramming;
                Fill(new Rect(x - 46, y - 13, 92, 23), rushing ? Brick : Gold);
                Center(x, y - 10, rushing ? "RAM!" : "LINING UP", fine, Panel);
                return;
            }
            Fill(new Rect(x - 20, y - 9, 40, 7), Panel);
            Fill(new Rect(x - 18, y - 7, 36 * encounter.Truck.ArmorFraction, 3), Brick);
        }

        private static string IntentLabel(TruckIntent intent) => intent switch
        {
            TruckIntent.LiningUp => "LINING UP",
            TruckIntent.Ramming => "RAMMING",
            TruckIntent.Recovering => "RECOVERING",
            TruckIntent.Pursuing => "GIVING CHASE",
            TruckIntent.Wrecked => "WRECKED",
            _ => "PATROLLING"
        };

        private static string SurfaceLabel(SurfaceKind? kind) => kind switch
        {
            SurfaceKind.Asphalt => "ASPHALT",
            SurfaceKind.Grass => "GRASS",
            SurfaceKind.Mud => "MUD",
            SurfaceKind.Sand => "SAND",
            SurfaceKind.Water => "CREEK",
            _ => "YARD SURFACE"
        };

        private static Color SurfaceColor(SurfaceKind? kind) => kind switch
        {
            SurfaceKind.Asphalt => YardRouteMap.Asphalt,
            SurfaceKind.Grass => Green,
            SurfaceKind.Mud => new Color(0.71f, 0.36f, 0.21f),
            SurfaceKind.Sand => Gold,
            SurfaceKind.Water => YardRouteMap.Creek,
            _ => Empty
        };

        private static GUIStyle Style(int size, FontStyle emphasis) => new(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = emphasis,
            padding = new RectOffset(),
            clipping = TextClipping.Overflow
        };

        private static void Card(float x, float y, float w, float h, Color accent)
        {
            Fill(new Rect(x + 3, y + 4, w, h), new Color(0, 0, 0, 0.31f));
            Fill(new Rect(x, y, w, h), Panel);
            Fill(new Rect(x, y, 4, h), accent);
            Fill(new Rect(x + 4, y + h - 1, w - 4, 1), Empty);
        }

        private static void Bar(float x, float y, float w, float h, float fraction, Color color, int count)
        {
            float cell = w / count;
            for (int index = 0; index < count; index++)
            {
                Rect block = new(x + cell * index, y, cell - 3, h);
                Fill(block, Empty);
                block.width *= Mathf.Clamp01(fraction * count - index);
                Fill(block, color);
            }
        }

        internal static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void Text(float x, float y, string value, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(new Rect(x, y, 760, style.fontSize + 5), value, style);
        }

        private static void Center(float center, float y, string value, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            float width = style.CalcSize(new GUIContent(value)).x;
            GUI.Label(new Rect(center - width * 0.5f, y, width, style.fontSize + 5), value, style);
        }
    }
}
