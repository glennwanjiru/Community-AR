using System.Collections;
using UnityEngine;
using UnityEngine.UI; // For legacy InputField and Text
using Firebase.Extensions;
using Firebase.Auth;
using Firebase;
using UnityEngine.SceneManagement; // For scene transitions

public class EmailPassLogin : MonoBehaviour
{
    #region variables
    [Header("Login")]
    public InputField LoginEmail;
    public InputField loginPassword;

    [Header("Sign up")]
    public InputField SignupEmail;
    public InputField SignupPassword;
    public InputField SignupPasswordConfirm;

    [Header("Status")]
    public Text statusText; // Legacy Text component for status updates

    [Header("Buttons")]
    public Button loginButton; // Button to trigger Login
    public Button signupButton; // Button to trigger SignUp

    [Header("Scene")]
    public string sceneNameToLoad = "MainScene"; // Name of the scene to load after successful login/signup
    #endregion

    #region Unity Methods
    // Start is called before the first frame update
    void Start()
    {
        // Add listeners to the buttons to call the respective methods when clicked
        loginButton.onClick.AddListener(Login);
        signupButton.onClick.AddListener(SignUp);

        // Hide password fields
        loginPassword.contentType = InputField.ContentType.Password;
        SignupPassword.contentType = InputField.ContentType.Password;
        SignupPasswordConfirm.contentType = InputField.ContentType.Password;
    }
    #endregion

    #region signup 
    public void SignUp()
    {
        SetStatusText("Signing up... Please wait.");

        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        string email = SignupEmail.text;
        string password = SignupPassword.text;

        if (password != SignupPasswordConfirm.text)
        {
            SetStatusText("Passwords do not match. Please try again.");
            return;
        }

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsCanceled)
            {
                SetStatusText("Sign-up canceled. Please try again later.");
                return;
            }
            if (task.IsFaulted)
            {
                SetStatusText("Error during sign-up. Check your email format and try again.");
                return;
            }

            // Firebase user has been created.
            AuthResult result = task.Result;

            SignupEmail.text = "";
            SignupPassword.text = "";
            SignupPasswordConfirm.text = "";

            if (result.User.IsEmailVerified)
            {
                SetStatusText("Sign up successful! You can now log in.");
                LoadScene();
            }
            else
            {
                SetStatusText("Please verify your email before logging in.");
                SendEmailVerification();
            }
        });
    }

    public void SendEmailVerification()
    {
        StartCoroutine(SendEmailForVerificationAsync());
    }

    IEnumerator SendEmailForVerificationAsync()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user != null)
        {
            var sendEmailTask = user.SendEmailVerificationAsync();
            yield return new WaitUntil(() => sendEmailTask.IsCompleted);

            if (sendEmailTask.Exception != null)
            {
                SetStatusText("Error sending verification email. Please try again.");
            }
            else
            {
                SetStatusText("Verification email sent. Please check your inbox.");
            }
        }
        else
        {
            SetStatusText("No user is logged in to send a verification email.");
        }
    }
    #endregion

    #region Login
    public void Login()
    {
        SetStatusText("Logging in... Please wait.");

        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        string email = LoginEmail.text;
        string password = loginPassword.text;

        Credential credential = EmailAuthProvider.GetCredential(email, password);
        auth.SignInAndRetrieveDataWithCredentialAsync(credential).ContinueWithOnMainThread(task => {
            if (task.IsCanceled)
            {
                SetStatusText("Login canceled. Please try again later.");
                return;
            }
            if (task.IsFaulted)
            {
                SetStatusText("Login failed. Check your credentials and try again.");
                return;
            }

            // Firebase user signed in successfully
            AuthResult result = task.Result;

            if (result.User.IsEmailVerified)
            {
                SetStatusText("Login successful! Welcome back.");
                LoadScene();
            }
            else
            {
                SetStatusText("Your email is not verified. Please verify your email first.");
            }
        });
    }
    #endregion

    #region UI Update Methods
    void SetStatusText(string msg)
    {
        statusText.text = msg;
    }

    void LoadScene()
    {
        // Load the desired scene after login or registration
        SceneManager.LoadScene(sceneNameToLoad);
    }
    #endregion
}
