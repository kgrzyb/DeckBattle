namespace DeckBattle
{
    public static class StatusReferenceFormatter
    {
        public static string Format(StatusDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName;
            }

            return definition.Kind.ToString();
        }
    }
}
