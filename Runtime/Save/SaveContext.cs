namespace Hwi.Foundation.Save
{
    public static class SaveContext
    {
        public static ISaveStore Default { get; set; } = new PlayerPrefsSaveStore();
    }
}
