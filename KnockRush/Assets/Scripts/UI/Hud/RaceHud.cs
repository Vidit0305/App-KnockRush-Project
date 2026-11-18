using KnockRush.Core.Events;
using KnockRush.Maps;
using UnityEngine;
using UnityEngine.UI;

namespace KnockRush.UI.Hud
{
    /// <summary>
    /// Race HUD: 3-2-1-GO countdown, race timer, checkpoint toast and finish banner.
    /// Placeholder styling (legacy Text) until the Phase 8 UI art pass; logic stays.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaceHud : MonoBehaviour
    {
        [SerializeField] private Text countdownText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text bannerText;
        [SerializeField] private Text toastText;

        private float countdownHideAt;
        private float toastHideAt;

        private void OnEnable()
        {
            EventBus.Subscribe<RaceCountdownEvent>(OnCountdown);
            EventBus.Subscribe<RaceFinishedEvent>(OnFinished);
            EventBus.Subscribe<CheckpointReachedEvent>(OnCheckpoint);
            EventBus.Subscribe<ParticipantFellEvent>(OnFell);
            EventBus.Subscribe<RaceTimeUpEvent>(OnTimeUp);
            SetText(bannerText, string.Empty);
            SetText(toastText, string.Empty);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RaceCountdownEvent>(OnCountdown);
            EventBus.Unsubscribe<RaceFinishedEvent>(OnFinished);
            EventBus.Unsubscribe<CheckpointReachedEvent>(OnCheckpoint);
            EventBus.Unsubscribe<ParticipantFellEvent>(OnFell);
            EventBus.Unsubscribe<RaceTimeUpEvent>(OnTimeUp);
        }

        private void Update()
        {
            var session = OfflineRaceSession.Current;
            if (session != null && timerText != null)
            {
                var remaining = Mathf.Max(0f, session.TimeLimit - session.ElapsedTime);
                timerText.text = FormatTime(session.ElapsedTime) + $"   <size=28>limit {FormatTime(remaining)}</size>";
            }

            if (countdownText != null && countdownHideAt > 0f && Time.time >= countdownHideAt)
            {
                countdownText.text = string.Empty;
                countdownHideAt = 0f;
            }

            if (toastText != null && toastHideAt > 0f && Time.time >= toastHideAt)
            {
                toastText.text = string.Empty;
                toastHideAt = 0f;
            }
        }

        public static string FormatTime(float seconds)
        {
            var minutes = Mathf.FloorToInt(seconds / 60f);
            return $"{minutes}:{seconds - minutes * 60f:00.00}";
        }

        private void OnCountdown(RaceCountdownEvent e)
        {
            SetText(countdownText, e.SecondsLeft > 0 ? e.SecondsLeft.ToString() : "GO!");
            countdownHideAt = e.SecondsLeft > 0 ? 0f : Time.time + 0.8f;
        }

        private void OnFinished(RaceFinishedEvent e)
        {
            SetText(bannerText, e.Qualified
                ? $"QUALIFIED!\n<size=40>#{e.Placement}  ·  {FormatTime(e.Time)}</size>"
                : $"ELIMINATED\n<size=40>#{e.Placement}</size>");
        }

        private void OnCheckpoint(CheckpointReachedEvent e)
        {
            ShowToast($"Checkpoint {e.Order}");
        }

        private void OnFell(ParticipantFellEvent e)
        {
            ShowToast("Whoops!");
        }

        private void OnTimeUp(RaceTimeUpEvent e)
        {
            SetText(bannerText, "TIME UP!");
        }

        private void ShowToast(string message)
        {
            SetText(toastText, message);
            toastHideAt = Time.time + 1.5f;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}
