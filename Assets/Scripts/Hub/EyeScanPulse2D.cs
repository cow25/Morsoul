using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Animates the Golem's Eye scan feedback over a target point: a sweeping
    /// sonar pulse travelling from the Golem's eye to the target, framed by
    /// pulsing scan brackets and a vertical scanline across the concealed prop.
    /// </summary>
    public class EyeScanPulse2D : MonoBehaviour
    {
        public Transform scanOrigin;
        public Transform scanTarget;
        public SpriteRenderer reticleRenderer;
        public SpriteRenderer sweepRenderer;
        public SpriteRenderer waveRenderer;

        [Header("Animation")]
        public float sweepAmplitude = 0.55f;
        public float sweepFrequency = 5.0f;
        public float waveInterval = 0.75f;
        public Color scanColor = new Color(0.28f, 0.96f, 1f, 0.95f);

        float _elapsed;
        SpriteRenderer _targetRenderer;

        public void Configure(Transform origin, Transform target)
        {
            scanOrigin = origin;
            scanTarget = target;
            _targetRenderer = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
            _elapsed = 0f;
            SyncPosition();
            TickVisuals(0f);
        }

        void OnEnable()
        {
            _targetRenderer = scanTarget != null ? scanTarget.GetComponentInChildren<SpriteRenderer>() : null;
            _elapsed = 0f;
            SyncPosition();
            TickVisuals(0f);
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            SyncPosition();
            TickVisuals(_elapsed);
        }

        Vector3 GetTargetCenter()
        {
            if (_targetRenderer != null)
            {
                var c = _targetRenderer.bounds.center;
                return new Vector3(c.x, c.y, -0.05f);
            }
            if (scanTarget != null)
                return new Vector3(scanTarget.position.x, scanTarget.position.y + 0.85f, -0.05f);
            return transform.position;
        }

        void SyncPosition()
        {
            if (scanTarget != null || _targetRenderer != null)
                transform.position = GetTargetCenter();
        }

        void TickVisuals(float t)
        {
            Vector3 targetCenter = GetTargetCenter();

            if (reticleRenderer != null)
            {
                float pulse = 1f + 0.05f * Mathf.Sin(t * 8f);
                reticleRenderer.transform.localScale = new Vector3(pulse, pulse, 1f);
                Color c = scanColor;
                c.a = 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(t * 4.5f));
                reticleRenderer.color = c;
            }

            if (sweepRenderer != null)
            {
                float y = Mathf.Sin(t * sweepFrequency) * sweepAmplitude;
                sweepRenderer.transform.localPosition = new Vector3(0f, y, -0.01f);
                Color c = scanColor;
                c.a = 0.82f + 0.18f * Mathf.Cos(t * sweepFrequency * 2f);
                sweepRenderer.color = c;
            }

            if (waveRenderer != null)
            {
                float phase = waveInterval > 0.01f ? (t % waveInterval) / waveInterval : 0f;
                Vector3 originPos = scanOrigin != null
                    ? new Vector3(scanOrigin.position.x + 0.15f, scanOrigin.position.y + 0.8f, -0.06f)
                    : targetCenter + Vector3.left * 1.8f;

                Vector3 wavePos = Vector3.Lerp(originPos, targetCenter, phase);
                wavePos.z = -0.06f;
                waveRenderer.transform.position = wavePos;

                float scale = Mathf.Lerp(0.45f, 1.25f, phase);
                waveRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                Color c = scanColor;
                c.a = Mathf.Sin(phase * Mathf.PI) * 0.92f;
                waveRenderer.color = c;
            }
        }
    }
}
