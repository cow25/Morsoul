using System;

namespace Morsoul.Data
{
    [Serializable]
    public class CausalPromptData
    {
        public string question;
        public string[] options;
        public int correctIndex;
        public string hintText;

        public bool IsEmpty => string.IsNullOrEmpty(question);
    }
}
