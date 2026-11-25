namespace TalentScout.Data.Entities;

public enum CandidateSearchStopReason
{
    TargetReached,
    ResultsExhausted,
    ScanLimitReached,
    PageLimitReached,
    RepeatedCursor
}