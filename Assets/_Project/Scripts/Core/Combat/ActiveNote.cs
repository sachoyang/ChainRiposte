namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// 지금 화면에 그려져야 하는 노트 하나. 뷰는 이걸 읽어 수축하는 원을 그린다.
    /// 연속기에서는 여러 개가 동시에 살아 있으므로, 하나짜리 이벤트가 아니라 목록으로 노출한다.
    /// </summary>
    public readonly struct ActiveNote
    {
        public BossNoteConfig Note { get; }

        /// <summary>0 = 예비동작 시작(원이 가장 큼), 1 = 타격 시점(원이 보스에 닿음).</summary>
        public float Progress { get; }

        /// <summary>타격까지 남은 시간(초). 여러 노트를 그릴 때 그리는 순서를 정하는 데 쓴다.</summary>
        public float SecondsUntilHit { get; }

        public ActiveNote(BossNoteConfig note, float progress, float secondsUntilHit)
        {
            Note = note;
            Progress = progress;
            SecondsUntilHit = secondsUntilHit;
        }
    }
}
