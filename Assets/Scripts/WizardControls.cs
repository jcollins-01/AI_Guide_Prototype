using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenAI;
using OpenAI.Models;
using OpenAI.Chat;
using OpenAI.Completions;
using System.Threading.Tasks;

public class WizardControls : MonoBehaviour
{
    // Variables to hold the scripts we access as the wizard
    private QueryDescription m_QueryDescriptionScript;
    private AutomaticGuide m_AutomatedGuideScript;
    private OpenAIQueries m_OpenAIQueriesScript;

    // String for the text to speech message when prompted by wizard
    public string m_TextToSpeechMessage;

    // Start is called before the first frame update
    void Start()
    {
        // Add script to use OpenAI API
        m_OpenAIQueriesScript = gameObject.AddComponent(typeof(OpenAIQueries)) as OpenAIQueries;

        // Find existing scripts that are necessary
        m_QueryDescriptionScript = FindObjectOfType(typeof(QueryDescription)) as QueryDescription;
        m_AutomatedGuideScript = FindObjectOfType(typeof(AutomaticGuide)) as AutomaticGuide;

        if (m_QueryDescriptionScript == null || m_AutomatedGuideScript == null)
        {
            Debug.LogWarning("One or more required scripts for WizardControls has not been found - please ensure that the GameObject with WizardControls also has QueryDescription and AutomaticGuide");
        }
        else
        {
            Debug.Log("WizardControls are active - ready for the wizard to intervene at any time!");
            // Description of the controls the wizard can use
            Debug.Log("Press space to call a test CV query on the scene from the guide's camera");
            // Ex. press left arrow to capture a picture to the left and query it
            Debug.Log("Drag a target game object into the Wizard Controls editor and press g to move the guide to that target, or t to teleport");
            
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Call a computer vision query on the scene from various directions
        if (Input.GetKeyDown("left"))
        {
            Debug.Log("Wizard called a CV query for the left side of the scene");
            m_QueryDescriptionScript.QueryLeft();
        }

        if (Input.GetKeyDown("right"))
        {
            Debug.Log("Wizard called a CV query for the right side of the scene");
            m_QueryDescriptionScript.QueryRight();
        }

        if (Input.GetKeyDown("up"))
        {
            Debug.Log("Wizard called a CV query for the front of the scene");
            m_QueryDescriptionScript.QueryFront();
        }

        if (Input.GetKeyDown("down"))
        {
            Debug.Log("Wizard called a CV query for behind the user in the scene");
            m_QueryDescriptionScript.QueryBehind();
        }

        /*if (Input.GetKeyDown("space"))
        {
            Debug.Log("Wizard called a CV query for the entire scene");
            StartCoroutine(m_QueryDescriptionScript.QueryScene());
        }*/

        // Call a pathfinding algorithm to guide the user to a specific object
        if (Input.GetKeyDown("g"))
        {
            Debug.Log("Wizard called a pathfind to a target object");
            m_AutomatedGuideScript.GuideToPosition();
        }

        // Call a position change to teleport the user to a specific object
        if (Input.GetKeyDown("t"))
        {
            Debug.Log("Wizard called a teleport to a target object");
            m_AutomatedGuideScript.TeleportToPosition();
        }
    }
}

public class OpenAIQueries : MonoBehaviour
{
    public static OpenAIClient client { get; set; }

    // OpenAI API key
    public string apiKey;

    // Strings to hold the different pieces of the query message
    public string userQuery = "What's going on in here?";
    private string playerClassification = "Imagine that the player is the yellow pill-shaped object in the lower left corner of this image. ";
    private string objectClassifications = "The upright, yellow cube is named Tall Building. The upright, green cube is named Short Building. The red cylinder to the right of Tall Building is named Red Car Back. The green cylinder next to Tall Building is named Green Car. The long, yellow cube laying on its side is named Sideways Building. The red cyilnder in front of Sideways Building is named Red Car Front. The green, flattened oval in the back is named Landmark. ";
    private string queryClassifications = "If the player seems like they want to describe the entire scene, then describe the scene as though you are helping the player understand the game they are in. If the player seems like they want to describe a particular object in the scene, describe the object in the image they are referring to. If the player seems like they want to go to a particular object in the scene, tell me only the name of the object in the image they would be referring to - ONLY DO THIS IF YOU'RE SURE THE PLAYER WANTS TO TRAVEL TO THAT OBJECT, and provide a description of the object if you aren't sure.";

    // Full message for OpenAI and OpenAI result
    private string text;
    public string result;

    private void Start()
    {
        Debug.Log("OpenAI is ready to be queried");

        // Create an instance of the OpenAI client
        client = new OpenAIClient(apiKey);

        // Default query to begin with
        text = playerClassification + objectClassifications + "Imagine the player said this: " + userQuery + ". " + queryClassifications;

        Debug.Log("Current query is: " + userQuery);
        Debug.Log("Press space to query with this message or alter user query field, then hit space");
    }

    public void Update()
    {
        // Check if the space bar is pressed
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Grab the newest userQuery value if it has changed
            text = playerClassification + objectClassifications + "Imagine the player said this: " + userQuery + ". " + queryClassifications;
            // Call the CallCompletion method with your desired userInput
            var result = CallCompletion(text);
        }
    }

    public async Task<string> CallCompletion(string userInput)
    {
        // Create the content for the message
        List<Content> content = new List<Content>
        {
            new Content(ContentType.Text, userInput),
            new Content(ContentType.ImageUrl, "https://i.postimg.cc/wMmyKDRz/Bird-s-Eye.png")
        };

        // Create the message to send to the API
        var chatPrompts = new List<Message>
        {
            new(Role.User, content),
        };

        var chatRequest = new ChatRequest(chatPrompts, model: "gpt-4-vision-preview", maxTokens: 300);
        string output = "N/A";
        try
        {
            var chatResponse = await client.ChatEndpoint.GetCompletionAsync(chatRequest);
            output = chatResponse.FirstChoice.ToString();
            Debug.Log("Response from GPT-4: " + output);
            result = output;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Exception in CallCompletion:\n" + e);
        }
        return output;
    }
}