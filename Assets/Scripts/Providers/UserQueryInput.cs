using UnityEngine;

public class UserQueryInput : MonoBehaviour
{
    [SerializeField][TextArea] private string debugQuery = "Where is my class?";

    public string GetCurrentQuery()
    {
        return debugQuery;
    }
}