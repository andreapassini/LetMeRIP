using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class CharacterListItem : MonoBehaviour
{
    // [SerializeField] private string characterName;
    public Image characterImage;
    [SerializeField] private TMP_Text characterNameText;


    public void Start()
    {
        this.characterImage = gameObject.GetComponent<Image>();
        
    }
    
    
    public void Init(Texture characterImageTexture, string characterName)
    {
        this.characterImage = gameObject.GetComponent<Image>();
        this.characterImage.image = characterImageTexture;
        this.characterNameText.text = characterName;
    }

    public void Select()
    {
        CharacterSelection.Instance.SelectCharacter(this);
        characterImage.tintColor = Color.green;
    }

    public void Unselect()
    {
        characterImage.tintColor = Color.white;
    }
}