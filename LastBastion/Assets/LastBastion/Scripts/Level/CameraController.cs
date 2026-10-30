using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Strategy camera: WASD / arrow keys to pan, mouse wheel to zoom toward the cursor,
    /// middle-mouse drag to pan. Stays inside the map bounds.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public float MinSize = 4.5f;
        public float MaxSize = 11f;

        Camera cam;
        Rect bounds;
        Vector3 basePos;
        Vector3 dragAnchor;
        bool dragging;
        float shakeTime, shakeDuration, shakeAmp;

        public void Init(Camera camera, Rect mapBounds)
        {
            cam = camera;
            bounds = mapBounds;
            basePos = cam.transform.position;
        }

        public void Focus(Vector2 p)
        {
            basePos = new Vector3(p.x, p.y, -10f);
        }

        public void Shake(float duration, float amplitude)
        {
            shakeDuration = shakeTime = duration;
            shakeAmp = amplitude;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            var lm = LevelManager.I;
            bool locked = lm != null && (lm.State == GameState.Victory || lm.State == GameState.Defeat);
            float dt = Time.unscaledDeltaTime;
            cam.transform.position = basePos;

            if (!locked)
            {
                Vector2 move = Vector2.zero;
                if (GameInput.KeyHeld(GKey.W) || GameInput.KeyHeld(GKey.Up)) move.y += 1f;
                if (GameInput.KeyHeld(GKey.S) || GameInput.KeyHeld(GKey.Down)) move.y -= 1f;
                if (GameInput.KeyHeld(GKey.D) || GameInput.KeyHeld(GKey.Right)) move.x += 1f;
                if (GameInput.KeyHeld(GKey.A) || GameInput.KeyHeld(GKey.Left)) move.x -= 1f;
                if (move.sqrMagnitude > 0f)
                {
                    Vector2 delta = move.normalized * cam.orthographicSize * 1.4f * dt;
                    basePos += new Vector3(delta.x, delta.y, 0f);
                }

                float scroll = UI.Busy ? 0f : GameInput.Scroll;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    Vector3 before = cam.ScreenToWorldPoint(GameInput.MousePosition);
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * 0.9f, MinSize, MaxSize);
                    Vector3 after = cam.ScreenToWorldPoint(GameInput.MousePosition);
                    basePos += new Vector3(before.x - after.x, before.y - after.y, 0f);
                    cam.transform.position = basePos;
                }

                if (GameInput.MiddleHeld)
                {
                    Vector3 cur = cam.ScreenToWorldPoint(GameInput.MousePosition);
                    if (!dragging) { dragging = true; dragAnchor = cur; }
                    else basePos += new Vector3(dragAnchor.x - cur.x, dragAnchor.y - cur.y, 0f);
                }
                else dragging = false;
            }

            basePos = Clamp(basePos);
            Vector3 shake = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                Vector2 r = Random.insideUnitCircle * shakeAmp * Mathf.Clamp01(shakeTime / shakeDuration);
                shake = new Vector3(r.x, r.y, 0f);
            }
            cam.transform.position = basePos + shake;
        }

        Vector3 Clamp(Vector3 p)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            p.x = bounds.width <= halfW * 2f ? bounds.center.x : Mathf.Clamp(p.x, bounds.xMin + halfW, bounds.xMax - halfW);
            p.y = bounds.height <= halfH * 2f ? bounds.center.y : Mathf.Clamp(p.y, bounds.yMin + halfH, bounds.yMax - halfH);
            p.z = -10f;
            return p;
        }
    }
}
