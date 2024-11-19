using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Firebase;
using Firebase.Auth;
using UnityEngine.SceneManagement; // For scene switching

public class LoginUser : MonoBehaviour
{
    public InputField emailInputField;
    public InputField passwordInputField;
    public Button loginButton;
    public Text feedbackText;

    private FirebaseAuth auth;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        loginButton.onClick.AddListener(LoginExistingUser);
    }

    void LoginExistingUser()
    {
        string email = emailInputField.text;
        string password = passwordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            feedbackText.text = "Please fill in all fields.";
            return;
        }

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                feedbackText.text = "Login canceled.";
                return;
            }
            if (task.IsFaulted)
            {
                feedbackText.text = "Error: " + task.Exception.InnerExceptions[0].Message;
                return;
            }

            // Success
            feedbackText.text = "Login successful!";
            SceneManager.LoadScene("DashboardScene"); // Replace with your scene name
        });
    }
}
