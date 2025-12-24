using NHunspell;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Input;
using System;
using System.IO;

namespace SpellTextBox
{
    public class SpellChecker : INotifyPropertyChanged
    {
        private SpellTextBox box;
        private Hunspell hunSpell;
        private List<Word> words;
        private ObservableCollection<Word> misspelledWords;
        private ObservableCollection<Word> suggestedWords;
        private List<Word> ignoredWords;
        private Word selectedMisspelledWord;
        private Word selectedSuggestedWord;

        public SpellChecker(Hunspell HunSpell, SpellTextBox Parent)
        {
            hunSpell = HunSpell; 
            box = Parent;
            Words = new List<Word>();
            MisspelledWords = new ObservableCollection<Word>();
            IgnoredWords = new List<Word>();
            SuggestedWords = new ObservableCollection<Word>();
        }

        public void Dispose()
        {
            if (hunSpell != null)
                hunSpell.Dispose();
        }

        public List<Word> Words
        {
            get { return words; }
            set
            {
                words = value;
                OnPropertyChanged("Words");
            }
        }

        public ObservableCollection<Word> MisspelledWords
        {
            get { return misspelledWords; }
            set
            {
                misspelledWords = value;
                OnPropertyChanged("MisspelledWords");
                if (SelectedMisspelledWord != null)
                    SelectedMisspelledWord = null;
            }
        }

        public ObservableCollection<MenuAction> MenuActions
        {
            get
            {
                List<MenuAction> commands = SuggestedWords.Select(w => new MenuAction()
                {
                    Name = w.Text,
                    Command = new DelegateCommand(
                        delegate
                        {
                            box.ReplaceSelectedWord(w);
                        })
                }).ToList();

                if (commands.Count == 0)
                {
                    commands.Add(new MenuAction()
                    {
                        Name = StringResources.Copy,
                        Command = ApplicationCommands.Copy
                    });
                    commands.Add(new MenuAction()
                    {
                        Name = StringResources.Cut,
                        Command = ApplicationCommands.Cut
                    });
                    commands.Add(new MenuAction()
                    {
                        Name = StringResources.Paste,
                        Command = ApplicationCommands.Paste
                    });
                }
                else
                {
                    commands.Add(new MenuAction()
                    {
                        Name = StringResources.AddCustom,
                        Command = new DelegateCommand(
                            delegate
                            {
                                SaveToCustomDictionary(SelectedMisspelledWord);

                                box.FireTextChangeEvent();
                            })
                    });
                }

                return new ObservableCollection<MenuAction>(commands);
            }
        }

        public ObservableCollection<Word> SuggestedWords
        {
            get { return suggestedWords; }
            set
            {
                suggestedWords = value;
                OnPropertyChanged("SuggestedWords");
            }
        }

        public List<Word> IgnoredWords
        {
            get { return ignoredWords; }
            set
            {
                ignoredWords = value;
                OnPropertyChanged("IgnoredWords");
            }
        }

        public Word SelectedMisspelledWord
        {
            get { return selectedMisspelledWord; }
            set
            {
                selectedMisspelledWord = value;
                LoadSuggestions(value);
                OnPropertyChanged("SelectedMisspelledWord");
                OnPropertyChanged("IsReplaceEnabled");
            }
        }

        public Word SelectedSuggestedWord
        {
            get { return selectedSuggestedWord; }
            set
            {
                selectedSuggestedWord = value;
                OnPropertyChanged("SelectedSuggestedWord");
                OnPropertyChanged("IsReplaceEnabled");
            }
        }

        public void LoadSuggestions(Word misspelledWord)
        {
            if (misspelledWord != null)
            {
                SuggestedWords = new ObservableCollection<Word>(hunSpell.Suggest(misspelledWord.Text).Select(s => new Word(s, misspelledWord.Index)));
                if (SuggestedWords.Count == 0) SuggestedWords = new ObservableCollection<Word> { new Word(StringResources.NoSuggestions, 0) };
            }
            else
            {
                SuggestedWords = new ObservableCollection<Word>();
            }
            OnPropertyChanged("SuggestedWords");
        }

        public void ClearLists()
        {
            Words.Clear();
            MisspelledWords.Clear();
        }


        private static bool ContainChinese(string input)
        {
            string pattern = "[\u4e00-\u9fbb]";
            return Regex.IsMatch(input, pattern);
        }

        private static bool IScapital(string input)
        {
            string pattern = "^[A-Z]+$";
            return Regex.IsMatch(input, pattern);
        }




        public void CheckSpelling(string content)
        {
            if (box.IsSpellCheckEnabled)
            {
                ClearLists();

                var matches = Regex.Matches(content, @"[A-Za-z]+");

                foreach (Match match in matches)
                {
                    if (IScapital(match.Value.Trim()) == false)
                    {
                        Words.Add(new Word(match.Value.Trim(), match.Index));
                    }
                }

                foreach (var word in Words)
                {
                    word.Text = word.Text.ToLower();

                    bool isIgnored = IgnoredWords.Contains(word);
                    if (!isIgnored)
                    {
                        bool exists = hunSpell.Spell(word.Text);
                        if (exists)
                            IgnoredWords.Add(word);
                        else
                            MisspelledWords.Add(word);
                    }
                }

                OnPropertyChanged("MisspelledWords");
                OnPropertyChanged("IgnoredWords");
            }
        }

        public void LoadCustomDictionary()
        {         

            string[] strings = File.ReadAllLines(box.CustomDictionaryPath);
            foreach (var str in strings)
            {
                hunSpell.Add(str);
            }
        }

        public void SaveToCustomDictionary(Word word)
        {     
            hunSpell.Add(word.Text);
            IgnoredWords.Add(word);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName)
        {
            var handler = this.PropertyChanged;
            if (handler != null) this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
