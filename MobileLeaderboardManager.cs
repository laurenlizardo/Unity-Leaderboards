using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SocialPlatforms;
using GooglePlayGames;
using GooglePlayGames.BasicApi;

/*
 * ------------------------------------------------
 * INTRO
 * ------------------------------------------------
 * This script is meant to use Unity's Social API to obtain leaderboard data from either Game Center (iOS) or 
 * Google Play (Android) and to transfer that information to your own in-game leaderboard. Again, this script
 * handles data only; this script does not create and format an in-game leaderboard.
 * 
 * Both iOS and Android platforms use the SocialPlatforms namespace. Android requires additional namespaces (see ANDROID LEADERBOARD NOTES).
 * 
 * ------------------------------------------------
 * SCRIPT FLOW
 * ------------------------------------------------
 * 1. Player authentication
 * 2. Leaderboard instance creation
 * 3. Loading scores, ranks, and names from the platform's leaderboard
 * 4. Storing users' scores, ranks, and names into an array that can be referenced from a script that handles UI
 * 5. Score submission
 * 6. Debug.Log
 * 
 * ------------------------------------------------
 * ANDROID - GOOGLE PLAY GAMES SERVICES - NOTES
 * ------------------------------------------------
 * 
 *  - Import the Google Play Game Services plugin. Follow the instructions on that page to set up your project on Unity. That page also 
 *      has links to the Google Developer Console to set up your game for the Google Play Store.
 *  - Use the following namespaces:
 *      - UnityEngine.SocialPlatforms
 *      - GooglePlayGames
 *      - GooglePlayGames.BasicApi
 *  - As of Google Play Game Services build v0.9.64, there is a class called ClientImpl.cs that uses a reference with a Gravity type. However,
 *      this conflicts with Unity's own Gravity type and an error will occur. To resolve this error, change the Gravity type in
 *      ClientImpl.cs to BasicApi.Gravity.
 * 
 * ------------------------------------------------
 * LINKS
 * ------------------------------------------------
 * 
 * -----UNITY'S SOCIAL API-----
 * Unity Social API - Manual: https://docs.unity3d.com/Manual/net-SocialAPI.html
 * Unity Social API - Scripting: https://docs.unity3d.com/ScriptReference/Social.html
 * 
 * -----IOS-----
 * Unity GameCenterPlatform - Scripting: https://docs.unity3d.com/ScriptReference/SocialPlatforms.GameCenter.GameCenterPlatform.html
 * Leaderboard Setup for iOS in Unity - TheAppGuruz: http://www.theappguruz.com/blog/leaderboard-setup-for-ios-in-unity-with-new-ui-system-in-unity
 * Video Tutorial(s):
 *  - Very Simple Leaderboard - Creating Leaderboard for iOS: https://youtu.be/ceRjgD4btKM
 * 
 * -----ANDROID-----
 * Google Play Games Services Plugin: https://github.com/playgameservices/play-games-plugin-for-unity
 * Google Play Games Services Plugin - Implementation: https://github.com/playgameservices/play-games-plugin-for-unity#google-play-games-plugin-for-unity
 * Setting Up Google Play Games Services (in Google Play Console): https://developers.google.com/games/services/console/enabling
 * Video Tutorial(s):
 *  - Add Google Play Game Services to your Android Game in Unity - Leaderboards Tutorial: https://youtu.be/M6nwu00-VR4
 *  - Unity 2018 - Implementing google Play Services (Achievements and Leaderboards): https://youtu.be/kL_TqHWm5mA
 * 
 */

public class MobileLeaderboardManager : MonoBehaviour
{
    private static MobileLeaderboardManager s_instance;
    public static MobileLeaderboardManager Instance { get { return s_instance; } }

    private static readonly string IOSLeaderboardID = "yourIOSLeaderboardID";
    public string iosLeaderboardID = IOSLeaderboardID;
    
    private static readonly string GPGSLeaderboardID = "yourGooglePlayLeaderboardID";
    public string gpgsLeaderboardID = GPGSLeaderboardID;

    private ILeaderboard leaderboard;   // Reference needed to create an instance of the leaderboard
    private ILocalUser localUser;       // Reference to the local user (Social.localUser) once the user is authenticated

    // Use a struct to hold the user info needed for the leaderboard
    [System.Serializable]
    public struct UserData
    {
        public string name;
        public int rank, score;
    }

    public UserData[] userData;     // Stores the ALL the users/scores for the in-game leaderboard. Reference this array from a class that handles UI.
    private UserData localUserData; // Stores the local user's name, rank, and score so it could be located on the leaderboard
    private string[] users; // Stores the UserProfiles that the loaded scores belong to
    
    public Text debugText;  // For texting purposes only.
    // Note: The method that uses the debugText variable checks if there is a reference to it in the Inspector before carrying out the instruction so there are no null references.

    private void Awake()
    {
        // Initialize the singleton
        s_instance = this;
    }

    private void Start()
    {
#if !UNITY_EDITOR
        // Authenticate the user when the game starts
        ProcessAuthentication();
#endif
    }

#region ----------Leaderboard Creation, Loading Scores and Users

    //-----------------------------------------
    // Private Methods
    //-----------------------------------------

    private void ProcessAuthentication()
    {
#if UNITY_ANDROID
        PlayGamesClientConfiguration config = new PlayGamesClientConfiguration.Builder().Build();
        PlayGamesPlatform.InitializeInstance(config);
        PlayGamesPlatform.Activate();
#endif

#if !UNITY_EDITOR
        Social.localUser.Authenticate((bool success) =>
        {
            if (success)
            {
                DisplayDebugLog("Authentication successful.");

                InitUser();
                CreateLeaderboardInstance();
            }
            else
            {
                DisplayDebugLog("Failed to authenticate user.");
            }

            // Note: It would be a good idea to have a UI element notifying the player to connect to Game Center if they failed to be authenticated
        });
#endif
    }

    private void InitUser()
    {
        // Store a reference to the user
        localUser = Social.localUser;

        DisplayDebugLog("Local user's ID: " + localUser.id.ToString());
        DisplayDebugLog("Local user's username: " + localUser.userName.ToString());
    }

    private void CreateLeaderboardInstance()
    {
        /* 
         * Note:
         * CreateLeaderboardInstance() can be called elsewhere in case the player's user data changes during gameplay.
         * It may be best to connect to the button that will display the leaderboard or it can be called right after the
         * player submits a new high score to the leaderboard, which might make the most sense because then it will re-evaluate
         * the user's data and load the appropriate rank range of the leaderboard.
         * 
         */

        DisplayDebugLog("Creating an instance of the Game Center leaderboard...");

        // Create an instance of the Game Center leaderboard using the ILeaderboard leaderboard variable and initialize its id and range
        leaderboard = Social.CreateLeaderboard();

#if UNITY_ANDROID
        leaderboard.id = gpgsLeaderboardID;
#endif

#if UNITY_IOS
        leaderboard.id = iosLeaderboardID;
#endif
        
        // Set the desired range for the leaderboard
        leaderboard.range = LocalTen(); 

        DisplayDebugLog("Leaderboard instance created.");

        // Load the leaderboard scores once the leaderboard has been initialized
        ProcessLoadedScores();

        // Note: Scores are loaded before the user profiles because the order/ranking is based on scores, not usernames
    }

    // This a range I specifically made for a game. Feel free to set the range to whatever you want.
    private Range LocalTen()
    {
        //Create a reference to the local user's rank
        int localUserRank = leaderboard.localUserScore.rank;

        // Tasks: 
        // Check if the local user's rank is in the top 10
        // If it is, set the leaderboard rank range from 1 to 10
        // Otherwise, set the rank range from the rank of local user minus 9 to the rank of the local user

        if (localUserRank <= 10)
        {
            leaderboard.range = new Range(1, 10);
            DisplayDebugLog("Leaderboard instance successfully created.");
        }
        else
        {
            leaderboard.range = new Range((localUserRank - 9), localUserRank);
            DisplayDebugLog("Leaderboard range set from " + (localUserRank - 9).ToString() + "to" + localUserRank.ToString());
        }

#if UNITY_ANDROID
        DisplayDebugLog("Leaderboard ID: " + gpgsleaderboardID.ToString() + ", Leaderboard Range: " + leaderboard.range.ToString());
#endif

#if UNITY_IOS
        DisplayDebugLog("Leaderboard ID: " + iosLeaderboardID.ToString() + ", Leaderboard Range: " + leaderboard.range.ToString());
#endif

        return leaderboard.range;
    }
    
    private void ProcessLoadedScores()  // Processes the scores from the leaderboard instance
    {
        leaderboard.LoadScores((bool success) =>
        {
            IScore[] scores = leaderboard.scores;   // Represents the scores within the leaderboard range; this is already ordered from high to low as set in iTunes and Google Play
            int numScores = scores.Length;          // References the above array's length
            users = new string[numScores];          // Stores the usernames that the scores belong to. This will be used to load the users in once the scores are loaded.

            // Iterate through the scores within the leaderboard range and gather the scores and ranks to store in userData[]
            for (int i = 0; i < numScores; i++)
            {
                // This info will be stored in the userInfos[] array
                userData[i].score = (int)scores[i].value;
                userData[i].rank = scores[i].rank;

                // This array will be transferred to ProcessLoadedUsers
                users[i] = scores[i].userID;
            }

            DisplayDebugLog("Scores successfully loaded on the leaderboard.");

            // Load the user profiles that the scores belong to once the scores are loaded
            ProcessLoadedUsers();
        });
    }
    
    private void ProcessLoadedUsers() // Processes the users based on the processed scores
    {
        Social.LoadUsers(users, (IUserProfile[] userProfiles) =>
        {
            //
            DisplayDebugLog("Length of UserData[]: " + userData.Length.ToString());

            // Iterate through the userData[] array within the leaderboard range to gather data to store scores and ranks in userData[]
            for (int i = 0; i < userProfiles.Length; i++)   // Use this array to populate your in-game leaderboard UI
            {
                userData[i].name = userProfiles[i].userName;

                DisplayDebugLog("User Profile " + (i + 1) + ", " +
                            "\n" + "Name: " + userData[i].name.ToString() + ", " +
                            "\n" + "Rank: " + userData[i].rank.ToString() + ", " +
                            "\n" + "Score: " + userData[i].score.ToString());
            }
        });
        
        DisplayDebugLog("Users successfully loaded on the leaderboard.");
    }

    //-----------------------------------------
    // Public Methods
    //-----------------------------------------

    public void DisplayDefaultLeaderboard()    // This displays the default leaderboard for Game Center and Google Play Games Services
    {
        Social.ShowLeaderboardUI();
    }
    // Note: To pass a specific leaderboard and not the default leaderboard, pass the leaderboard ID when calling that function
    // (ie: Social.ShowLeaderboardUI("LeaderboardID")). If you are building to Android, use the PlayGamesPlatform's function (ie: 
    // PlayGamesPlatform.Instance.ShowLeaderboardUI("LeaderboardID")).

#endregion

#region ----------Score Submission

    //-----------------------------------------
    // Public Methods
    //-----------------------------------------

    public void SubmitScore(int score, string id)   // Call this method to submit a score only if the user has beaten their old high score.
    {
        // When calling this method, specify the score to submit and the ID of the leaderboard to submit the score to.
        // This will work for both iOS and Android devices.
        
        // Tasks:
        // Check if the player is authenticated and submit the score to the leaderboard
        // If the player is not authenticated, re-authenticate and try to submit the score again

        if (localUser.authenticated)
        {
            // Tasks:
            // Submit the score to the leaderboard
            // Create a new instance of the leaderboard

            Social.ReportScore(score, id, (bool success) =>
            {
                if (success)
                {
                    DisplayDebugLog("Score was successfully submitted to the leaderboard: " + id);
                    DisplayDebugLog("Score submitted by " + localUser.userName.ToString() + ": " + score.ToString());

                    CreateLeaderboardInstance();    // Call CreateLeaderboardInstance() again to re-evaluate the updated leaderboard and use the new leaderboard instance data to transfer to your in-game leaderboard
                }
            });

            DisplayDebugLog("Processing score submission...");
        }
        else
        {
            // Re-authenticate the user
            localUser.Authenticate((bool success) =>
            {
                DisplayDebugLog("Re-authenticating the player...");

                if (success)
                {
                    // Tasks:
                    // Submit the score to the leaderboard
                    // Create a new instance of the leaderboard
                    
                    Social.ReportScore(score, id, (bool success) =>
                    {
                        if (success)
                        {
                            DisplayDebugLog("Score was successfully submitted to the leaderboard: " + id);
                            DisplayDebugLog("Score submitted by " + localUser.userName.ToString() + ": " + score.ToString());

                            CreateLeaderboardInstance();    // Call CreateLeaderboardInstance() again to re-evaluate the updated leaderboard and use the new leaderboard instance data to transfer to your in-game leaderboard
                        }
                    });
                }
                else
                    DisplayDebugLog("Authentication failed. Score not submitted to the leaderboard.");
            });
        }
    }

#endregion

#region ----------Debug.Log
    
    private void DisplayDebugLog(string data)   
    {
        // This method displays the Debug.Log() as a string if there is a reference to the debugText text component

        Debug.Log(data);

        if (debugText != null)
            debugText.text += ("\n" + data);

        /*
         * Notes:
         * Configure the Text component to always show the latest Debug.Log statement
         * - Stretch the RectTransform
         * - Align text to left, anchor to bottom
         * - Set horizontal overflow to wrap
         * - Set vertical overflow to overflow
         *      
         */
    }

#endregion
}
