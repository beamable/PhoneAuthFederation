using Beamable;
using Beamable.Common;
using Beamable.Server.Clients;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhoneFederationTest : MonoBehaviour
{
    public TMP_InputField phoneInputField;
    public TMP_InputField codeInputField;
    public TMP_Text phoneNumber;
    public Button sendCodeButton;
    public Button verifyCodeButton;
    public Button detachButton;

    private Promise<string> _smsPromise;

    async void Start()
    {
        phoneInputField.enabled = false;
        codeInputField.enabled = false;
        verifyCodeButton.enabled = false;
        detachButton.enabled = false;
        sendCodeButton.enabled = false;

        sendCodeButton.onClick.AddListener(OnSendCodeButtonPressed);
        verifyCodeButton.onClick.AddListener(OnVerifyCodeButtonPressed);
        detachButton.onClick.AddListener(OnDetachButtonPressed);

        var ctx = BeamContext.Default;
        await ctx.OnReady;
        await ctx.Accounts.OnReady;
        ctx.Accounts.Current.OnUpdated += OnAccountUpdated;

        OnAccountUpdated();
    }

    void OnAccountUpdated()
    {
        Debug.Log("Account Updated!");
        var ctx = BeamContext.Default;
        
        if(ctx.Accounts.Current.TryGetExternalIdentity<PhoneNumberIdentity, PhoneAuthFederationClient>(out var identity))
        {
            phoneNumber.text = identity.userId;
            detachButton.enabled = true;
            sendCodeButton.enabled = false;
            phoneInputField.enabled = false;
        } 
        else
        {
            phoneNumber.text = "";
            detachButton.enabled = false;
            sendCodeButton.enabled = true;
            phoneInputField.enabled = true;
        }
    }

    public async void OnSendCodeButtonPressed()
    {
        var ctx = BeamContext.Default;
        var phoneNumber = phoneInputField.text;
        if(string.IsNullOrEmpty(phoneNumber))
        {
            Debug.LogError("Phone Number is empty.");
            return;
        }

        var isAvailable = await ctx.Accounts.IsExternalIdentityAvailable<PhoneNumberIdentity, PhoneAuthFederationClient>(phoneNumber);
        if (!isAvailable)
        {
            Debug.LogError("Phone Number is already attached to another account.");
            return;
        }

        var result = await ctx.Accounts.AddExternalIdentity<PhoneNumberIdentity, PhoneAuthFederationClient>(phoneNumber, OnCodeSent);
        if(result.isSuccess)
        {
            Debug.Log("Successfully associated phone number.");
        }
        else
        {
            Debug.LogError($"Failed to associated phone number: {result.error}");
        }
    }

    public void OnVerifyCodeButtonPressed()
    {
        var phoneNumber = phoneInputField.text;
        var smsCode = codeInputField.text;

        if(string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(smsCode))
        {
            Debug.LogError("Phone Number and SMS Code must not be empty.");
            return;
        }

        if(_smsPromise == null)
        {
            Debug.LogError("SMS Promise is null");
            return;
        }

        _smsPromise.CompleteSuccess(smsCode);

        phoneInputField.text = "";
        codeInputField.text = "";
        codeInputField.enabled = false;
        verifyCodeButton.enabled = false;
    }

    public async void OnDetachButtonPressed()
    {
        var ctx = BeamContext.Default;
        await ctx.Accounts.Current.RemoveExternalIdentity<PhoneNumberIdentity, PhoneAuthFederationClient>();
    }

    private Promise<string> OnCodeSent(string challengeToken)
    {
        Debug.Log($"On Code Sent: {challengeToken}");
        codeInputField.enabled = true;
        verifyCodeButton.enabled = true;

        _smsPromise = new Promise<string>();
        return _smsPromise;
    }
}
