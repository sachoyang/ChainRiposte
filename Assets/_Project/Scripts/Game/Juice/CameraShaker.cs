using System.Collections;
using UnityEngine;

namespace ChainRiposte.Game.Juice
{
    /// <summary>
    /// 카메라 흔들림. 히트스톱(timeScale 저하) 중에도 흔들리도록 unscaled 시간으로 진행한다.
    /// 요청이 겹치면 더 큰 진폭이 이긴다.
    /// </summary>
    public sealed class CameraShaker : MonoBehaviour
    {
        private Vector3 _basePosition;
        private Coroutine _routine;
        private float _activeAmplitude;

        public void Shake(float amplitude, float duration)
        {
            if (_routine != null)
            {
                if (amplitude < _activeAmplitude)
                    return; // 진행 중인 더 큰 흔들림 유지
                StopCoroutine(_routine);
                transform.localPosition = _basePosition;
            }

            _routine = StartCoroutine(ShakeRoutine(amplitude, duration));
        }

        private IEnumerator ShakeRoutine(float amplitude, float duration)
        {
            _activeAmplitude = amplitude;
            _basePosition = transform.localPosition;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float falloff = 1f - t / duration;
                transform.localPosition = _basePosition +
                    (Vector3)(Random.insideUnitCircle * (amplitude * falloff));
                yield return null;
            }

            transform.localPosition = _basePosition;
            _routine = null;
            _activeAmplitude = 0f;
        }
    }
}
