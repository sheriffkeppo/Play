using System.Linq;
using UnityEngine.UIElements;

public class ButtonGroup : VisualElement
{
    public enum ButtonGroupDirection { Horizontal, Vertical }
    
    // UIToolkit factory class
    public new class UxmlFactory : UxmlFactory<ButtonGroup, UxmlTraits> {};
    public new class UxmlTraits : VisualElement.UxmlTraits
    {
        private readonly UxmlEnumAttributeDescription<ButtonGroupDirection> direction = new() { name = "direction", defaultValue = ButtonGroupDirection.Vertical};

        public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
        {
            base.Init(ve, bag, cc);
            ButtonGroup target = ve as ButtonGroup;

            // Read additional attributes from XML
            // In the UIBuilder, the XML attributes and target object fields are synchronized implicitly by name.
            target.Direction = direction.GetValueFromBag(bag, cc);
        }
    }

    private ButtonGroupDirection direction;
    public ButtonGroupDirection Direction
    {
        get => direction;
        set
        {
            direction = value;
            if (direction == ButtonGroupDirection.Horizontal)
            {
                this.AddToClassList("horizontal");
                this.RemoveFromClassList("vertical");
            }
            else if (direction == ButtonGroupDirection.Vertical)
            {
                this.AddToClassList("vertical");
                this.RemoveFromClassList("horizontal");
            }
        }
    }
    
    private VisualElement firstChild;
    private VisualElement lastChild;
    
    public ButtonGroup()
    {
        this.RegisterCallback<GeometryChangedEvent>(UpdateChildClasses);
        Direction = ButtonGroupDirection.Vertical;
    }

    private void UpdateChildClasses(GeometryChangedEvent evt)
    {
        VisualElement newFirstChild = this.Children().FirstOrDefault();
        if (newFirstChild != firstChild)
        {
            newFirstChild?.AddToClassList("firstChild");
            firstChild?.RemoveFromClassList("firstChild");
            firstChild = newFirstChild;
        }
        
        VisualElement newLastChild = this.Children().LastOrDefault();
        if (newLastChild != lastChild)
        {
            newLastChild?.AddToClassList("lastChild");
            lastChild?.RemoveFromClassList("lastChild");
            lastChild = newLastChild;
        }
    }
}
