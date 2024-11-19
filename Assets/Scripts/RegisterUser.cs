using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Firebase;
using Firebase.Auth;
using UnityEngine.SceneManagement; // For scene switching

public class RegisterUser : MonoBehaviour
{
    public InputField emailInputField;
    public InputField passwordInputField;
    public Button registerButton;
    public Text feedbackText;

    private FirebaseAuth auth;

    void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
        registerButton.onClick.AddListener(RegisterNewUser);
    }

    void RegisterNewUser()
    {
        string email = emailInputField.text;
        string password = passwordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            feedbackText.text = "Please fill in all fields.";
            return;
        }

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                feedbackText.text = "Registration canceled.";
                return;
            }
            if (task.IsFaulted)
            {
                feedbackText.text = "Error: " + task.Exception.InnerExceptions[0].Message;
                return;
            }

            // Success
            feedbackText.text = "Registration successful!";
            SceneManager.LoadScene("HomeScene"); // Replace with your scene name
        });
    }
}
