using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Firebase.Auth;
using System.Threading.Tasks;

public class AuthManager : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseUser user;
    [Header("Registration UI")]
    public InputField registerEmailInput;
    public InputField registerPasswordInput;
    public Button registerButton;
    [Header("Login UI")]
    public InputField loginEmailInput;
    public InputField loginPasswordInput;
    public Button loginButton;
    [Header("Feedback")]
    public Text feedbackText;
    [Header("Scene Names")]
    public string registerSuccessScene;
    public string loginSuccessScene;

    void Start()
    {
        InitializeFirebase();
        // Add listeners for buttons
        registerButton.onClick.AddListener(RegisterUser);
        loginButton.onClick.AddListener(LoginUser);
    }

    private void InitializeFirebase()
    {
        auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;
        AuthStateChanged(this, null);
    }

    private void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        if (auth.CurrentUser != user)
        {
            bool signedIn = user != auth.CurrentUser && auth.CurrentUser != null;
            if (!signedIn && user != null)
            {
                Debug.Log("Signed out: " + user.UserId);
            }
            user = auth.CurrentUser;
            if (signedIn)
            {
                Debug.Log("Signed in: " + user.UserId);
            }
        }
    }

    public void RegisterUser()
    {
        string email = registerEmailInput.text;
        string password = registerPasswordInput.text;

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                ShowFeedback("Registration canceled.");
                return;
            }
            if (task.IsFaulted)
            {
                ShowFeedback("Registration error: " + task.Exception.Flatten().InnerExceptions[0].Message);
                return;
            }

            // Correctly get the FirebaseUser from the AuthResult
            FirebaseUser newUser = task.Result.User;
            Debug.LogFormat("User registered successfully: {0} ({1})", newUser.DisplayName, newUser.UserId);

            // Load scene on main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                SceneManager.LoadScene(registerSuccessScene);
            });
        });
    }

    public void LoginUser()
    {
        string email = loginEmailInput.text;
        string password = loginPasswordInput.text;

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                ShowFeedback("Login canceled.");
                return;
            }
            if (task.IsFaulted)
            {
                ShowFeedback("Login error: " + task.Exception.Flatten().InnerExceptions[0].Message);
                return;
            }

            // Correctly get the FirebaseUser from the AuthResult
            FirebaseUser loggedInUser = task.Result.User;
            Debug.LogFormat("User signed in successfully: {0} ({1})", loggedInUser.DisplayName, loggedInUser.UserId);

            // Load scene on main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                SceneManager.LoadScene(loginSuccessScene);
            });
        });
    }

    private void ShowFeedback(string message)
    {
        // Ensure UI updates happen on main thread
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            feedbackText.text = message;
        });
        Debug.Log(message);
    }

    private void OnDestroy()
    {
        auth.StateChanged -= AuthStateChanged;
        auth = null;
    }
}

// Utility class to run code on Unity's main thread
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private System.Collections.Concurrent.ConcurrentQueue<System.Action> _executionQueue =
        new System.Collections.Concurrent.ConcurrentQueue<System.Action>();

    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("UnityMainThreadDispatcher");
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
        return _instance;
    }

    void Update()
    {
        while (_executionQueue.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    public void Enqueue(System.Action action)
    {
        _executionQueue.Enqueue(action);
    }
}