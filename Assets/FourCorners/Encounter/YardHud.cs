using UnityEngine;

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

        private GUIStyle title;
        private GUIStyle label;
        private GUIStyle fine;
        private GUIStyle number;

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

            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 810f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));

            Card(22, 21, 293, 66, Gold);
            Text(37, 27, "FOUR CORNERS", title, Cream);
            Text(38, 58, "SALVAGE YARD", label, Gold);

            float opponentX = width - 265;
            Card(opponentX, 21, 243, 66, Brick);
            string status = encounter.Truck.Destroyed ? "WRECKED" : truckDriver.IsChasing ? "GIVING CHASE" : "PATROLLING";
            Text(opponentX + 14, 29, "WRECKER", label, Cream);
            Text(opponentX + 118, 30, status, fine, Brick);
            Bar(opponentX + 15, 59, 212, 13, encounter.Truck.ArmorFraction, Brick, 10);

            float bottom = height - 98;
            Card(22, bottom, 327, 76, Blue);
            int kmh = Mathf.RoundToInt(encounter.Player.Vehicle.Speed * 3.6f);
            Text(36, bottom + 7, kmh.ToString("00"), number, Cream);
            Text(37, bottom + 47, "KM/H", fine, Dust);
            Text(123, bottom + 10, "MUSCLE  /  ARMOR", label, Cream);
            Bar(124, bottom + 44, 208, 15, encounter.Player.ArmorFraction, Green, 10);

            float weaponX = width - 285;
            Card(weaponX, bottom, 124, 76, Gold);
            Text(weaponX + 12, bottom + 8, "GUN", label, Cream);
            Text(weaponX + 12, bottom + 45, "LMB   /   ∞", fine, Gold);
            Card(weaponX + 135, bottom, 128, 76, Gold);
            Text(weaponX + 147, bottom + 8, "MINES", label, Cream);
            Text(weaponX + 147, bottom + 45, $"RMB   /   {encounter.Mines}", fine, Gold);

            DrawWreckerMarker(scale, height);
            DrawStatus(width, height);
            GUI.matrix = previous;
        }

        private void DrawStatus(float width, float height)
        {
            float middle = width * 0.5f;
            if (encounter.Result != EncounterResult.Active)
            {
                bool won = encounter.Result == EncounterResult.Won;
                Card(middle - 214, 21, 428, 84, won ? Gold : Brick);
                Center(middle, 29, won ? "WRECKER DOWN" : "YOU'RE WRECKED", title, Cream);
                Center(middle, 69, won ? "Explore the yard   ·   R Restart" : "R Restart encounter", label, Dust);
                return;
            }

            bool broken = encounter.Arena.Fence.IsBroken;
            Card(middle - 218, 22, 436, 35, broken ? Green : Gold);
            Center(middle, 30, broken ? "GATE BREACHED  /  SHORTCUT OPEN" : "ASPHALT LOOP  /  CREEK SHORTCUT", label, Cream);

            float bottom = height - 83;
            Fill(new Rect(middle - 226, bottom, 452, 56), Panel);
            string routeHint = !truckDriver.IsChasing ? "Get close to draw out the wrecker"
                : broken ? "Take another lap and follow your tracks" : "Bait the truck through the fence at the east bank";
            Center(middle, bottom + 8, routeHint, label, Gold);
            Center(middle, bottom + 33, "WASD Drive   ·   SPACE Handbrake   ·   R Restart", fine, Dust);
        }

        private void DrawWreckerMarker(float scale, float height)
        {
            if (encounter.Truck.Destroyed)
                return;

            Vector3 point = view.WorldToScreenPoint(encounter.Truck.transform.position + Vector3.up * 3.5f);
            if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height)
                return;

            float x = point.x / scale;
            float y = height - point.y / scale;
            Fill(new Rect(x - 20, y - 9, 40, 7), Panel);
            Fill(new Rect(x - 18, y - 7, 36 * encounter.Truck.ArmorFraction, 3), Brick);
        }

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

        private static void Fill(Rect rect, Color color)
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
