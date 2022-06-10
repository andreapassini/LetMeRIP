using System.Collections.Generic;
using UnityEngine;

public class CharacterSelection : MonoBehaviour
{
    public static CharacterSelection Instance;

    // public List<CharacterListItem> availableCharacters;
    [SerializeField] private CharacterListItem selectedCharacter;

    private void Awake()
    {
        Instance = this;
    }

    public void SelectCharacter(CharacterListItem character)
    {
        selectedCharacter.Unselect();
        selectedCharacter = character;
    }
}