using System.Collections.Generic;
using UnityEngine;

namespace FourCorners
{
    internal sealed class YardRouteMap
    {
        internal static readonly Color Asphalt = new(0.70f, 0.65f, 0.55f);
        internal static readonly Color Creek = new(0.30f, 0.76f, 0.73f);
        internal static readonly Color Ramp = new(1f, 0.71f, 0.37f);

        private readonly Vector2[] loop;
        private readonly Vector2[] shortcut;
        private readonly Vector2[] ramps;
        private readonly Vector2 gate;
        private readonly Vector2 center;
        private readonly Vector2 dimensions;
        private readonly Vector3 right;
        private readonly Vector3 ahead;

        public YardRouteMap(ArenaLayout arena, Camera camera)
        {
            right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            ahead = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            loop = ProjectRoute(arena.Loop);
            shortcut = ProjectRoute(arena.Shortcut);
            ramps = ProjectRoute(arena.Ramps);
            gate = Project(arena.Fence.transform.position);

            Vector2 low = loop[0];
            Vector2 high = low;
            foreach (Vector2 point in loop)
            {
                low = Vector2.Min(low, point);
                high = Vector2.Max(high, point);
            }
            foreach (Vector2 point in ramps)
            {
                low = Vector2.Min(low, point);
                high = Vector2.Max(high, point);
            }
            center = (high + low) * 0.5f;
            dimensions = high - low;
        }

        public void Draw(Rect area, Transform player, IReadOnlyList<CombatVehicle> opponents,
            Color playerColor, Color wreckColor, Color gateColor)
        {
            float scale = Mathf.Min(area.width / dimensions.x, area.height / dimensions.y);
            DrawRoute(loop, area.center, scale, 4, Asphalt, true);
            DrawRoute(shortcut, area.center, scale, 3, Creek, false);
            foreach (Vector2 ramp in ramps)
            {
                Vector2 point = Place(ramp, area.center, scale);
                Line(point + new Vector2(-3, 2), point + new Vector2(0, -3), 2, Ramp);
                Line(point + new Vector2(0, -3), point + new Vector2(3, 2), 2, Ramp);
            }

            Vector2 gatePoint = Place(gate, area.center, scale);
            Line(gatePoint - Vector2.up * 5, gatePoint + Vector2.up * 5, 4, gateColor);
            for (int index = 0; index < opponents.Count; index++)
            {
                CombatVehicle rival = opponents[index];
                if (rival.Destroyed)
                {
                    Vector2 point = PlaceVehicle(rival.transform, area, scale);
                    Line(point - new Vector2(3, 3), point + new Vector2(3, 3), 2, wreckColor);
                    Line(point - new Vector2(3, -3), point + new Vector2(3, -3), 2, wreckColor);
                }
                else
                {
                    DrawVehicle(rival.transform, area, scale, YardHud.OpponentColor(index));
                }
            }
            DrawVehicle(player, area, scale, playerColor);
        }

        private void DrawRoute(Vector2[] route, Vector2 origin, float scale, float width, Color color, bool closed)
        {
            int edges = closed ? route.Length : route.Length - 1;
            for (int index = 0; index < edges; index++)
            {
                Vector2 a = Place(route[index], origin, scale);
                Vector2 b = Place(route[(index + 1) % route.Length], origin, scale);
                Line(a, b, width, color);
            }
        }

        private void DrawVehicle(Transform vehicle, Rect area, float scale, Color color)
        {
            Vector2 point = PlaceVehicle(vehicle, area, scale);
            Vector2 direction = Project(vehicle.forward).normalized;
            Vector2 side = new(-direction.y, direction.x);
            Vector2 nose = point + direction * 6;
            Vector2 tail = point - direction * 4;
            Color outline = new(0.05f, 0.04f, 0.03f);
            Line(tail - side * 3.5f, nose, 4.5f, outline);
            Line(tail + side * 3.5f, nose, 4.5f, outline);
            Line(tail - side * 3.5f, nose, 2.5f, color);
            Line(tail + side * 3.5f, nose, 2.5f, color);
        }

        private Vector2 PlaceVehicle(Transform vehicle, Rect area, float scale)
        {
            Vector2 point = Place(Project(vehicle.position), area.center, scale);
            return new Vector2(Mathf.Clamp(point.x, area.xMin, area.xMax), Mathf.Clamp(point.y, area.yMin, area.yMax));
        }

        private Vector2[] ProjectRoute(Transform[] markers)
        {
            if (markers == null)
                return System.Array.Empty<Vector2>();

            Vector2[] route = new Vector2[markers.Length];
            for (int index = 0; index < route.Length; index++)
                route[index] = Project(markers[index].position);
            return route;
        }

        private Vector2 Project(Vector3 world) => new(Vector3.Dot(world, right), -Vector3.Dot(world, ahead));

        private Vector2 Place(Vector2 point, Vector2 origin, float scale) => origin + (point - center) * scale;

        private static void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, from);
            YardHud.Fill(new Rect(from.x - width * 0.25f, from.y - width * 0.5f, delta.magnitude + width * 0.5f, width), color);
            GUI.matrix = previous;
        }
    }
}
