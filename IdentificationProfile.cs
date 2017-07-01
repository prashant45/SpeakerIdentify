namespace App326
{
  using System;

  enum EnrollmentStatus
  {
    Enrolling,
    Training,
    Enrolled
  }
  enum OperationStatus
  {
    NotStarted,
    Running,
    Failed,
    Succeeded
  }
  enum Confidence
  {
    Low,
    Normal,
    High
  }
  class Error
  {
    public string Code { get; set; }
    public string Message { get; set; }
  }
  class BaseResponse
  {
    public Error Error { get; set; }
  }
  class AddIdentificationProfileResponse : BaseResponse
  {
    public Guid IdentificationProfileId
    {
      get; set;
    }
  }
  class GetProfilesResponse : AddIdentificationProfileResponse
  {
    public string Locale { get; set; }
    public float EnrollmentSpeechTime { get; set; }
    public float RemainingEnrollmentSpeechTime { get; set; }
    public DateTimeOffset CreatedDateTime { get; set; }
    public DateTimeOffset LastActionDateTime { get; set; }
    public EnrollmentStatus EnrollmentStatus { get; set; }
  }
  class ProcessingResult
  {
    public Guid IdentifiedProfileId { get; set; }
    public Confidence  Confidence { get; set; }
    public EnrollmentStatus EnrollmentStatus { get; set; }
    public float EnrollmentSpeechTime { get; set; }
    public float RemainingEnrollmentSpeechTime { get; set; }
    public float SpeechTime { get; set; }
  }
  class GetOperationStatusResponse : BaseResponse
  {
    public OperationStatus Status { get; set; }
    public DateTimeOffset CreatedDateTime { get; set; }
    public DateTimeOffset LastActionDateTime { get; set; }
    public ProcessingResult ProcessingResult { get; set; }

  }
}