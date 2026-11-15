namespace SelfTracker.Domain.Common;

public enum DdiaEdition { Unconfirmed, First, Second }

public enum PauseReason { Illness, Travel, WorkSpike, FamilyEvent, Other }

public enum DayType { Full, Mvd, Rest }

/// <summary>Display state of a calendar date (BR-05). Never rendered as a failure.</summary>
public enum DayState { NotStarted, Full, Mvd, Rest, Missed, Paused, Future, Today, Unclassified }

public enum TickType { Dsa, Core, Build }

public enum TimeCategory { Dsa, DdiaDesign, MainTrack, Review, Mocks, Applications, Other }

public enum TimeSource { Manual, Timer, Derived }

public enum SchedulePhase { A, B, C }

public enum GymStatus { Done, Modified, Skipped, Rest, Walk, Zone2 }

public enum PlanTrack { Dsa, Ddia, React, MeaiMaf, Design, Flagship, Scale, Interview, Review, Other }

public enum PlanTaskKind { Manual, DerivedDsa, DerivedDdia }

public enum PlanTaskStatus { NotStarted, InProgress, Done, Deferred, Skipped }

public enum CheckpointOutcome { OnTrack, Slip, Replan }

public enum FileKind { Document, Image, Audio, Video, Code, Archive, Other }

public enum TravelRole { None, PreplannedMvd, TimedAttempt, Review, ShortReview }
