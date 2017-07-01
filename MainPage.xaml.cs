namespace App326
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.ApplicationModel.DataTransfer;
    using Windows.Storage;
    using Windows.System;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
    enum VisualStates
    {
        Recording,
        Submitting,
        Default
    }
    public event PropertyChangedEventHandler PropertyChanged;


    public MainPage()
    {
        this.InitializeComponent();
        this.restClient = new OxfordSpeakerIdRestClient();
        this.DataContext = this;
    }

    public string DisplayText
    {
        get
        {
            return (this.displayText);
        }
        set
        {
            if (this.displayText != value)
            {
                this.displayText = value;
                this.RaisePropertyChanged("DisplayText");
            }
        }
    }

    public float ProgressValue
    {
        get
        {
            return (this.progressValue);
        }
        set
        {
            if (this.progressValue != value)
            {
                this.progressValue = value;
                this.RaisePropertyChanged("ProgressValue");
            }
        }
    }

    public float ProgressMinimum
    {
        get
        {
            return (this.progressMinimum);
        }
        set
        {
            if (this.progressMinimum != value)
            {
                this.progressMinimum = value;
                this.RaisePropertyChanged("ProgressMinimum");
            }
        }
    }


    public float ProgressMaximum
    {
        get
        {
            return (this.progressMaximum);
        }
        set
            {
            if (this.progressMaximum != value)
            {
                this.progressMaximum = value;
                this.RaisePropertyChanged("ProgressMaximum");
            }
        }
    }

    protected async override void OnNavigatedTo(NavigationEventArgs e)
    {
        this.conversation = new Conversation(this.mediaElement);
        await this.DoHelloAsync();
    }

    void RaisePropertyChanged(string property)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    async Task DoHelloAsync()
    {
        this.SwitchState(VisualStates.Default);

        string response = string.Empty;

        await this.DisplayAndSay("please say hello")
        .WaitForCommandsAsync("hello");

        await this.DoCheckEnrollmentsAsync();
    }

    async Task DoCheckEnrollmentsAsync()
    {
        var sayTask = this.DisplayAndSay("checking the web for data, two seconds");

        var checkTask = this.restClient.GetIdentificationProfilesAsync();

        await Task.WhenAll(sayTask, checkTask);

        var results = checkTask.Result;

        this.currentProfiles = results.ToList();

        var count = this.currentProfiles.Where(
        r => r.EnrollmentStatus == EnrollmentStatus.Enrolled).Count();

        /*await this.conversation
        .SayAsync($"I found {count} fully enrolled records")
        .PauseAsync(TimeSpan.FromSeconds(1))
        .SayAsync(count > 0 ? "would you like to check or enroll?" : "let's enroll you");
        */

        if (count == 0)
        {
            await this.DoEnrollAsync();
        }
        else
        {
          this.DisplayText = "Checking for the audio";
        await this.DoCheckAsync();
        /*
        switch (option.Text)
        {
            case "check":
            
            break;
            case "enroll":
            await this.DoEnrollAsync();
            break;
            default:
            break;
        }*/
        }

    }

    async Task DoCheckAsync()
    {
        await this.conversation.SayAsync(
        "Introduce yourself in 2-3 sentences");

        var profilesToMatchAgainst =
            this.currentProfiles
            .Where(p => p.EnrollmentStatus == EnrollmentStatus.Enrolled)
            .Select(p => p.IdentificationProfileId)
            .Take(MAX_PROFILES_COUNT);

        var response = await this.SubmitSpeechForProfileAsync(
            OxfordSpeakerIdRestClient.GetUriForProfileIdentification(profilesToMatchAgainst),
            REQUIRED_SPEECH_TIME,
            true);

        if (response.Status == OperationStatus.Succeeded)
        {
        if (response.ProcessingResult.IdentifiedProfileId == Guid.Empty)
        {
            await this.DisplayAndSay("the call worked but we did not identify you");
        }
        else
        {
            // TODO:
            // We dont need to copt to clipboard
            // We identify the Subject ID == Name and  post some extra information 
            //this.AddToClipboard(response.ProcessingResult.IdentifiedProfileId);

          /* await this.DisplayAndSay(
           $"we identified you with {response.ProcessingResult.Confidence} confidence")
           .PauseAsync(TimeSpan.FromSeconds(1))
           .SayAsync("I've put the id on your clipboard");
           */
          string currentID = response.ProcessingResult.IdentifiedProfileId.ToString();

          if (currentID == Nik)
          {
            await this.DisplayAndSay("Hi Nikos");
          }
          else if (currentID == Rahul)
          {
            await this.DisplayAndSay("Hi Rahul");
          }
          else if (currentID == Prashant)
          {
            await this.DisplayAndSay("Hi Prashant");
          }


            // TODO: 
            // We need a id == name and some extra information 
        }
        }
        else
        {
        await this.DisplayAndSay("Sorry, that didn't work");
        }
        //this.DoCheckEnrollmentsAsync();
    }

    async Task<GetOperationStatusResponse> SubmitSpeechForProfileAsync(
        Uri processingEndpointUri,
        int speechTime,
        bool showBrowser)
    {
        GetOperationStatusResponse response = null;

        await this.CaptureVoiceAsync(speechTime, showBrowser);

        this.SwitchState(VisualStates.Submitting);

        // this.DisplayText = "submitting";

        using (var stream = await this.recordingFile.OpenReadAsync())
        {
        var statusEndpointUri = await this.restClient.PostSpeechStreamToProcessingEndpointAsync(
            processingEndpointUri, stream);

        //this.DisplayText = "checking status";

        //await this.conversation.SayAsync("Now going back to see what the status is");

        response = await this.restClient.GetStatusAsync(statusEndpointUri);

        while ((response.Status == OperationStatus.NotStarted) ||
            (response.Status == OperationStatus.Running))
        {
            //await this.DisplayAndSay("still processing, checking again in 5 seconds");

            await Task.Delay(TimeSpan.FromSeconds(5));

            response = await this.restClient.GetStatusAsync(statusEndpointUri);
        }
        }

        this.SwitchState(VisualStates.Default);

        return (response);
    }

        // Not add but compare with Names and display correcponding extra info
    void AddToClipboard(Guid guid)
    {
      DataPackage package = new DataPackage();
      package.SetText(guid.ToString());
      Clipboard.SetContent(package);
    }

    async Task DoEnrollAsync()
    {
        this.DisplayText = "making online profile";

        await this.conversation
        .SayAsync("I'm going to create an online profile for you");

        var newProfileResponse = await this.restClient.AddIdentifcationProfileAsync();

        this.DisplayText = "done";

        this.AddToClipboard(newProfileResponse.IdentificationProfileId);

        await this.conversation
        .SayAsync($"Ok, I made a profile for you")
        .PauseAsync(TimeSpan.FromMilliseconds(500))
        .SayAsync("I put the GUID for it on your clipboard")
        .PauseAsync(TimeSpan.FromMilliseconds(500))
        .SayAsync("let's now submit some audio of your speech");

        int speechTime = REQUIRED_SPEECH_TIME;

        while (speechTime > 0)
        {
        var endpointUri = OxfordSpeakerIdRestClient.GetUriForProfileEnrollment(
            newProfileResponse.IdentificationProfileId);

        var response = await this.SubmitSpeechForProfileAsync(
            endpointUri, speechTime, speechTime == REQUIRED_SPEECH_TIME);

        if (response.Status != OperationStatus.Succeeded)
        {
            await this.DisplayAndSay("sorry, that didn't work, let's try again");
        }
        else
        {
            if (response.ProcessingResult.EnrollmentStatus == EnrollmentStatus.Enrolled)
            {
            speechTime = 0;
            }
            else if (response.ProcessingResult.EnrollmentStatus == EnrollmentStatus.Enrolling)
            {
            speechTime = Math.Max(
                (int)response.ProcessingResult.RemainingEnrollmentSpeechTime,
                MINIMUM_SPEECH_TIME);

            await this.DisplayAndSay(
                $"that worked but we need more speech");
            }
        }
        }
        await this.DisplayAndSay("enrolled");

        this.SwitchState(VisualStates.Default);

        // not awaiting, yet...
        // this.DoCheckEnrollmentsAsync();
    }

    async Task CaptureVoiceAsync(int timeInSeconds, bool firstTime)
    {
        /*if (firstTime)
        {
        await Launcher.LaunchUriAsync(new Uri(READ_URL));
        this.DisplayText = "opening browser";
        await this.conversation
            .SayAsync("I'm going to open a browser tab for you")
            .PauseAsync(TimeSpan.FromSeconds(1));
        }
        await this.conversation.SayAsync(
        firstTime ?
            $"We need to capture {timeInSeconds} seconds of you reading" :
            $"ready to capture {timeInSeconds} more of you reading");*/

        var recorder = new AudioRecorder();

        this.recordingFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(RECORDING_FILE, CreationCollisionOption.ReplaceExisting);

        this.SwitchState(VisualStates.Recording);

        await recorder.StartRecordToFileAsync(this.recordingFile);

        var realTime = (int)(timeInSeconds * 1.2);

        this.ProgressValue = 0;
        this.ProgressMinimum = 0;
        this.ProgressMaximum = realTime;

        for (int i = 0; i < realTime; i++)
        {
        await Task.Delay(1000);
        this.ProgressValue++;
        }
        await recorder.StopRecordAsync();

        await this.DisplayAndSay("Ok, we've got your voice locally");

        this.SwitchState(VisualStates.Default);

    }

    Task<ConversationResult> DisplayAndSay(string text)
    {
        this.DisplayText = text;
        return (this.conversation.SayAsync(text));
    }
    void SwitchState(VisualStates state)
    {
        VisualStateManager.GoToState(this, state.ToString(), true);
    }

    float progressMaximum;
    string displayText;
    float progressValue;
    float progressMinimum;
    StorageFile recordingFile;
    List<GetProfilesResponse> currentProfiles;

    OxfordSpeakerIdRestClient restClient;
    Conversation conversation;
    static readonly int MAX_PROFILES_COUNT = 10;
    static readonly int REQUIRED_SPEECH_TIME = 30;
    static readonly int MINIMUM_SPEECH_TIME = 20;
    static readonly string RECORDING_FILE = "recording.bin";
    string Nik = "21647f63-be4a-46a9-b646-e35285a07686";
    string Rahul = "79036a15-a489-46c4-8097-b74c28219273";
    string Prashant = "d308e99e-f9e4-4066-b499-76f86f913af0";
    }
}
