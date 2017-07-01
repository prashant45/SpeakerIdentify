namespace App326
{
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Net.Http;
  using System.Text;
  using System.Threading.Tasks;
  using Windows.Storage.Streams;
  using System.IO;
  using System.Net.Http.Headers;

  // Lazy not turning this into a proper service and injecting it.
  class OxfordSpeakerIdRestClient
  {
    public OxfordSpeakerIdRestClient()
    {

    }
    byte[] HackOxfordWavPcmStream(IInputStream inputStream, out int offset)
    {
      var netStream = inputStream.AsStreamForRead();
      var bits = new byte[netStream.Length];
      netStream.Read(bits, 0, bits.Length);

      // original file length
      var pcmFileLength = BitConverter.ToInt32(bits, 4);

      // take away 36 bytes for the JUNK chunk
      pcmFileLength -= 36;

      // now copy 12 bytes from start of bytes to 36 bytes further on
      for (int i = 0; i < 12; i++)
      {
        bits[i + 36] = bits[i];
      }
      // now put modified file length into byts 40-43
      var newLengthBits = BitConverter.GetBytes(pcmFileLength);
      newLengthBits.CopyTo(bits, 40);

      // the bits that we want are now 36 onwards in this array
      offset = 36;

      return (bits);
    }
    public async Task<GetOperationStatusResponse> GetStatusAsync(Uri uri)
    {
      var result = await this.GetUriJsonResultAsync<GetOperationStatusResponse>(uri);

      return (result);
    }
    public static Uri GetUriForProfileIdentification(IEnumerable<Guid> profileIds)
    {
      var guidStrings = profileIds.Select(g => g.ToString());
      var joined = string.Join(",", guidStrings);

      var uri = new Uri(
        $"{OXFORD_BASE_URL}{OXFORD_IDENTIFY}" +
        Uri.EscapeDataString(joined));

      return (uri);
    }
    public static Uri GetUriForProfileEnrollment(Guid profileId)
    {
      var uri = new Uri(
       $"{OXFORD_BASE_URL}{OXFORD_IDENTIFICATION_PROFILES_ENDPOINT}/" +
       Uri.EscapeDataString(profileId.ToString()) +
       $"/{OXFORD_ENROLL}");

      return (uri);
    }
    public async Task<Uri> PostSpeechStreamToProcessingEndpointAsync(Uri uri,
      IInputStream inputStream)
    {
      var result = await this.SendPcmStreamToOxfordEndpointForHeaderAsync(
        uri, inputStream, "Operation-Location");

      return (new Uri(result));
    }
    async Task<string> SendPcmStreamToOxfordEndpointForHeaderAsync(
      Uri uri, IInputStream inputStream,string headerName)
    {
      var response = await this.SendPcmStreamToOxfordEndpointAsync(uri, inputStream);

      this.ThrowOnFailStatus(response, string.Empty);

      return (response.Headers.GetValues(headerName).FirstOrDefault());
    }
    async Task<HttpResponseMessage> SendPcmStreamToOxfordEndpointAsync(Uri uri,
      IInputStream inputStream)
    {
      int offset;
      byte[] bits = this.HackOxfordWavPcmStream(inputStream, out offset);

      ByteArrayContent content = new ByteArrayContent(bits, offset, bits.Length - offset);

      var response = await this.HttpClient.PostAsync(uri, content);

      return (response);
    }
    async Task<T> SendPcmStreamToOxfordEndpointAsync<T>(Uri uri, IInputStream inputStream)
    {
      var response = await this.SendPcmStreamToOxfordEndpointAsync(uri, inputStream);

      var result = await this.HandleHttpJsonResultAsync<T>(response);

      return (result);
    }
    void ThrowOnFailStatus(HttpResponseMessage response, string content)
    {
      if (!response.IsSuccessStatusCode)
      {
        throw new HttpRequestException(
          $"Something went wrong - I got [{content}]");
      }
    }
    public async Task<AddIdentificationProfileResponse> AddIdentifcationProfileAsync()
    {
      var jObject = new JObject();

      jObject["locale"] = "en-us";

      var result = await this.PostEndpointJsonResultAsync<AddIdentificationProfileResponse>(
        OXFORD_IDENTIFICATION_PROFILES_ENDPOINT, jObject);

      return (result);
    }
    public async Task<IEnumerable<GetProfilesResponse>> GetIdentificationProfilesAsync()
    {
      var results = await this.GetEndpointJsonResultAsync<GetProfilesResponse[]>(
        OXFORD_IDENTIFICATION_PROFILES_ENDPOINT);

      return (results);
    }
    async Task<T> PostEndpointJsonResultAsync<T>(string endpoint, JObject jsonObject)
    {
      var content = new StringContent(jsonObject.ToString(),
        Encoding.UTF8, "application/json");

      var response = await this.HttpClient.PostAsync(
        new Uri($"{OXFORD_BASE_URL}/{endpoint}"), content);

      var result = await this.HandleHttpJsonResultAsync<T>(response);

      return (result);
    }
    async Task<T> GetUriJsonResultAsync<T>(Uri uri)
    {
      var response = await this.HttpClient.GetAsync(uri);

      var result = await this.HandleHttpJsonResultAsync<T>(response);

      return (result);
    }
    Task<T> GetEndpointJsonResultAsync<T>(string endpoint)
    {
      return (this.GetUriJsonResultAsync<T>(
        new Uri($"{OXFORD_BASE_URL}/{endpoint}")));
    }
    async Task<T> HandleHttpJsonResultAsync<T>(HttpResponseMessage response)
    {
      var stringContent = await response.Content.ReadAsStringAsync();

      this.ThrowOnFailStatus(response, stringContent);

      var jsonObject = JsonConvert.DeserializeObject<T>(stringContent);

      return (jsonObject);
    }
    HttpClient HttpClient
    {
      get
      {
        if (this.httpClient == null)
        {
          this.httpClient = new HttpClient();
          this.httpClient.DefaultRequestHeaders.Add(
            OXFORD_SUB_KEY_HEADER, Keys.OxfordKey);
        }
        return (this.httpClient);
      }
    }
    static readonly string OXFORD_BASE_URL = "https://api.projectoxford.ai/spid/v1.0/";
    static readonly string OXFORD_ENROLL = "enroll";
    static readonly string OXFORD_IDENTIFY = "identify?identificationProfileIds=";
    static readonly string OXFORD_IDENTIFICATION_PROFILES_ENDPOINT = "identificationProfiles";
    static readonly string OXFORD_SUB_KEY_HEADER = "Ocp-Apim-Subscription-Key";
    HttpClient httpClient;
  }
}
