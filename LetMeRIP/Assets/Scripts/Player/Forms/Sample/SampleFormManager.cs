public class SampleFormManager : FormManager
{
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        forms.Add(gameObject.AddComponent<SampleForm1>());
        forms.Add(gameObject.AddComponent<SampleForm2>());


        // 0 => base class form
        SwitchForm(0);

        BindAbilities();
    }

    public override void BindAbilities()
    {
        base.BindAbilities();

        if (!photonView.IsMine) return;

        playerInputActions.Player.Transformation1.performed += ctx => SwitchForm(0);
        playerInputActions.Player.Transformation2.performed += ctx => SwitchForm(1);
    }
}