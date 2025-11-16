using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Player
{
	[System.Serializable]
	public class TimedVideo
	{
		public VideoClip clip;
		public float atSecond = 0f;
		public bool loop = true;
		[TextArea]
		public string subtitle;
	}

	public class Credits : MonoBehaviour
	{
		[Header("Scroll Settings")]
		public float scrollSpeed = 50f;

		[Header("References")]
		public RectTransform creditsContainer;
		public float loopHeight = 800f;

		[Header("Canvas Fade")]
		public CanvasGroup creditsCanvasGroup;

		[Header("Video Timeline")]
		public RawImage videoTarget;
		public VideoPlayer videoPlayer;
		public TimedVideo[] videoTimeline;
		public bool loopTimeline = true;
		public float timelineLengthSec = 60f;

		[Header("Subtitle")]
		public TextMeshProUGUI subtitleLabel;

		[Header("End Behavior")]
		public bool exitOnScrollEnd = false;
		public bool exitOnTimelineEnd = false;
		[Range(0f,10f)] public float fadeOutDuration = 1.5f;

		[Header("Audio Fade")]
		public bool useGlobalAudioFade = true;
		public AudioSource musicSource;

		private bool _isExiting;
		private Vector2 startPos;
		private float _elapsed;

		void Start()
		{
			startPos = creditsContainer.anchoredPosition;
			_elapsed = 0f;
			ApplyVideoForTime(0f, force:true);
		}

		void Update()
		{
			creditsContainer.anchoredPosition += Vector2.up * scrollSpeed * Time.deltaTime;

			if (creditsContainer.anchoredPosition.y >= loopHeight)
			{
				if (exitOnScrollEnd)
				{
					StartExitSequence();
					return;
				}
				else
				{
					creditsContainer.anchoredPosition = startPos;
				}
			}

			_elapsed += Time.deltaTime;
			float t = _elapsed;
			if (loopTimeline && timelineLengthSec > 0f)
			{
				t = Mathf.Repeat(t, timelineLengthSec);
			}
			else if (!loopTimeline && timelineLengthSec > 0f && _elapsed >= timelineLengthSec)
			{
				ApplyVideoForTime(timelineLengthSec, force:true);
				if (exitOnTimelineEnd)
				{
					StartExitSequence();
					return;
				}
			}
			ApplyVideoForTime(t, force:false);
			
			if (Keyboard.current.escapeKey.wasPressedThisFrame)
			{
				ExitGame();
			}
		}

		private VideoClip _currentVideo;
		
		private void ApplyVideoForTime(float seconds, bool force)
		{
			var bestVideo = SelectBestVideo(seconds);
			if (bestVideo != null && videoPlayer && videoTarget)
			{
				if (!videoTarget.enabled) videoTarget.enabled = true;

				if (force || _currentVideo != bestVideo.clip)
				{
					_currentVideo = bestVideo.clip;
					videoPlayer.isLooping = bestVideo.loop;
					videoPlayer.clip = _currentVideo;
					videoPlayer.Play();
				}

				if (subtitleLabel)
				{
					bool hasText = !string.IsNullOrEmpty(bestVideo.subtitle);
					subtitleLabel.text = hasText ? bestVideo.subtitle : string.Empty;
					subtitleLabel.enabled = hasText;
				}
				return;
			}
			
			if (videoTarget) videoTarget.enabled = false;
			if (subtitleLabel)
			{
				subtitleLabel.text = string.Empty;
				subtitleLabel.enabled = false;
			}
		}

		private void StartExitSequence()
		{
			if (_isExiting) return;
			_isExiting = true;
			StartCoroutine(CoExitSequence());
		}

		private System.Collections.IEnumerator CoExitSequence()
		{
			float dur = Mathf.Max(0f, fadeOutDuration);

			var cgFade = creditsCanvasGroup ? StartCoroutine(CoFadeCanvas(creditsCanvasGroup, 0f, dur)) : null;
			var audioFade = StartCoroutine(CoFadeAudio(0f, dur));

			float t = 0f;
			while (t < dur) { t += Time.unscaledDeltaTime; yield return null; }

			if (creditsCanvasGroup) creditsCanvasGroup.alpha = 0f;
			if (useGlobalAudioFade) AudioListener.volume = 0f;
			if (musicSource) musicSource.volume = 0f;

			ExitGame();
		}

		private System.Collections.IEnumerator CoFadeCanvas(CanvasGroup cg, float target, float dur)
		{
			float start = cg ? cg.alpha : 1f;
			float t = 0f;
			while (t < dur)
			{
				t += Time.unscaledDeltaTime;
				float k = dur > 0f ? t / dur : 1f;
				if (cg) cg.alpha = Mathf.Lerp(start, target, k);
				yield return null;
			}
			if (cg) cg.alpha = target;
		}

		private System.Collections.IEnumerator CoFadeAudio(float target, float dur)
		{
			float startGlobal = AudioListener.volume;
			float startSrc = musicSource ? musicSource.volume : 1f;
			float t = 0f;
			while (t < dur)
			{
				t += Time.unscaledDeltaTime;
				float k = dur > 0f ? t / dur : 1f;
				if (useGlobalAudioFade) AudioListener.volume = Mathf.Lerp(startGlobal, target, k);
				if (musicSource) musicSource.volume = Mathf.Lerp(startSrc, target, k);
				yield return null;
			}
			if (useGlobalAudioFade) AudioListener.volume = target;
			if (musicSource) musicSource.volume = target;
		}
		
		private TimedVideo SelectBestVideo(float seconds)
		{
			if (videoTimeline == null || videoTimeline.Length == 0)
				return null;

			TimedVideo best = null;
			for (int i = 0; i < videoTimeline.Length; i++)
			{
				var entry = videoTimeline[i];
				if (entry == null || entry.clip == null) continue;
				if (entry.atSecond <= seconds)
				{
					if (best == null || entry.atSecond > best.atSecond)
						best = entry;
				}
			}

			return best;
		}
	   
		private void ExitGame()
		{
			#if UNITY_EDITOR
				UnityEditor.EditorApplication.isPlaying = false;
			#else
				Application.Quit();
			#endif
		}
	}
}
