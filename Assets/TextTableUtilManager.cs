using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NM;
using SimpleFileBrowser;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using yutokun;

public class TextTableUtilManager : MonoBehaviour {
    public Button LoadTextTableButton;
    public Button LoadExtraLanguageButton;
    public Button SaveButton;
    public TMPro.TMP_Text Output;
    public Toggle LanguageSelector;

    public static string RawTextTable = string.Empty;

    private Dictionary<TextTable.SupportedLanguages, Dictionary<String, String>> allLanguages = new();
    private Dictionary<TextTable.SupportedLanguages, List<String>> missingIds = new();

    void Start() {
        LoadTextTableButton.onClick.AddListener( () => StartCoroutine( loadTextTable() ) );
        LoadExtraLanguageButton.onClick.AddListener( () => StartCoroutine( loadLanguageCsv() ) );
        LoadExtraLanguageButton.interactable = false;
        SaveButton.interactable = false;
        SaveButton.onClick.AddListener( () => StartCoroutine( saveTextTable() ) );

        Output.text = String.Empty;
        LanguageSelector.transform.parent.gameObject.SetActive( false );
    }

    IEnumerator loadTextTable() {
        FileBrowser.SetFilters( false, new FileBrowser.Filter( "json", ".json" ) );
        yield return FileBrowser.WaitForLoadDialog( FileBrowser.PickMode.Files, false, "C:\\dev\\MightierApp\\Assets\\Plugins\\NMPlatform\\Resources", "texttable.json", "Load Text Table" );
        if( FileBrowser.Success ) {
            string path = FileBrowser.Result[0];
            try {
                RawTextTable = System.IO.File.ReadAllText( path );
                Debug.Log( $"Got file {path} length {RawTextTable.Length} bytes" );
                bool result = TextTable.setCurrentLanguage( TextTable.SupportedLanguages.eng );
                if( result == true ) {
                    Output.text = $"Loaded {path}\nEnglish string count: {TextTable.GetTextCount()}";
                    LoadExtraLanguageButton.interactable = true;
                    SaveButton.interactable = true;
                    LanguageSelector.transform.parent.gameObject.SetActive( true );

                    // Save the English text
                    allLanguages[TextTable.SupportedLanguages.eng] = TextTable.TextTableData;

                    // Now go through other languages and save them all.
                    var languages = Enum.GetValues( typeof( TextTable.SupportedLanguages ) );
                    // get the index of eng in languages
                    int engIndex = Array.IndexOf( languages, TextTable.SupportedLanguages.eng );
                    for( int i = engIndex + 1; i < languages.Length; i++ ) {
                        TextTable.SupportedLanguages language = (TextTable.SupportedLanguages)languages.GetValue( i );
                        result = TextTable.setCurrentLanguage( language );
                        if( result == true ) {
                            int textCount = TextTable.GetTextCount();
                            allLanguages[language] = TextTable.TextTableData;
                            missingIds[language] = TextTable.TextTableMissingIds;

                            if( textCount > 10 ) {
                                Debug.Log( $"Missing Ids for {language}: {String.Join( ", ", missingIds[language] )}" );
                            }
                        }
                    }
                }

                setupLangugageToggles();
            } catch( System.Exception e ) {
                Debug.Log( "Error reading file: " + e.Message + " " + e.StackTrace );
            }
        } else {
            Debug.Log( "No file picked" );
        }
    }

    List<TextTable.SupportedLanguages> getSelectedLanguages() {
        List<TextTable.SupportedLanguages> languages = new();
        foreach( Transform child in LanguageSelector.transform.parent.transform ) {
            if( child.GetComponent<Toggle>().isOn ) {
                string languageCode = child.GetComponentInChildren<TMPro.TMP_Text>().text.Split( " " )[0];
                languages.Add( (TextTable.SupportedLanguages)Enum.Parse( typeof( TextTable.SupportedLanguages ), languageCode ) );
            }
        }

        return languages;
    }

    private void setupLangugageToggles() {
        // Remove existing language toggles.
        foreach( Transform child in LanguageSelector.transform.parent.transform ) {
            if( child != LanguageSelector.transform ) {
                Destroy( child.gameObject );
            }
        }

        var languages = Enum.GetValues( typeof( TextTable.SupportedLanguages ) );
        // get the index of eng in languages
        int engIndex = Array.IndexOf( languages, TextTable.SupportedLanguages.eng );
        int extraLanguageCount = 0;
        for( int i = engIndex; i < languages.Length; i++ ) {
            TextTable.SupportedLanguages language = (TextTable.SupportedLanguages)languages.GetValue( i );

            GameObject thisLanguageToggle = LanguageSelector.gameObject;
            if( extraLanguageCount > 0 ) {
                thisLanguageToggle = Instantiate( LanguageSelector.gameObject, LanguageSelector.transform.parent.transform );
            }

            extraLanguageCount++;

            thisLanguageToggle.GetComponent<Toggle>().isOn = false;
            TMPro.TMP_Text text = thisLanguageToggle.GetComponentInChildren<TMPro.TMP_Text>();
            text.text = $"{language.ToString()} {allLanguages[language].Count}";
            thisLanguageToggle.GetComponent<Toggle>().onValueChanged.AddListener( ( bool value ) => {
                if( getSelectedLanguages().Count > 0 ) {
                    LoadExtraLanguageButton.interactable = true;
                } else {
                    LoadExtraLanguageButton.interactable = false;
                }
            } );
        }

        LoadExtraLanguageButton.interactable = false;
    }

    IEnumerator loadLanguageCsv() {
        List<TextTable.SupportedLanguages> languages = getSelectedLanguages();

        FileBrowser.SetFilters( false, new FileBrowser.Filter( "csv", ".csv" ) );
        yield return FileBrowser.WaitForLoadDialog( FileBrowser.PickMode.Files, false, "~\\Downloads", "Ukrainian.csv", "Load Language csv" );
        if( FileBrowser.Success ) {
            string path = FileBrowser.Result[0];
            try {
                var languageSheet = CSVParser.LoadFromPath( path, Delimiter.Comma, Encoding.UTF8 );

                const String columnIdPrefix = "textTable/";
                Debug.Log( $"Got file {path} length {languageSheet.Count} rows" );
                int textIdColumn = -1;
                int[] languageColumns = new int[languages.Count];

                // First read the header row
                List<String> header = languageSheet[0];
                for( int i = 0; i < header.Count; i++ ) {
                    if( header[i].Contains( columnIdPrefix + "textId" ) ) {
                        textIdColumn = i;
                    } else {
                        for( int j = 0; j < languages.Count; j++ ) {
                            if( header[i] == columnIdPrefix + languages[j].ToString() ) {
                                languageColumns[j] = i;
                            }
                        }
                    }
                }

                String errorString = String.Empty;
                if( textIdColumn < 0 ) {
                    errorString += "Unable to find textId column in csv file. ";
                }

                for( int i = 0; i < languages.Count; i++ ) {
                    if( languageColumns[i] < 0 ) {
                        errorString += $"Unable to find {languages[i]} column in csv file. ";
                    }
                }

                if( errorString != String.Empty ) {
                    Output.text += $"\n<color=red>{errorString}</color>";
                    Debug.LogError( errorString );
                    yield break;
                }

                // Now read the csv and keep track of each string.
                for( int i = 1; i < languageSheet.Count; i++ ) {
                    List<String> row = languageSheet[i];
                    if( row.Count <= textIdColumn ) {
                        Debug.LogError( $"Error reading row {i}. Missing textId column." );
                        continue;
                    }

                    string textId = row[textIdColumn];
                    if( textId == String.Empty ) {
                        Debug.LogError( $"Error reading row {i}. Missing textId." );
                        continue;
                    }

                    string normalizedTextId = TextTable.normalizeTextId( textId );
                    for( int j = 0; j < languages.Count; j++ ) {
                        if( row.Count <= languageColumns[j] ) {
                            Debug.LogError( $"Error reading row {i}. Missing {languages[j]} column." );
                            continue;
                        }

                        string text = row[languageColumns[j]];
                        if( text != String.Empty ) {
                            allLanguages[languages[j]][normalizedTextId] = text;
                        }
                    }
                }

                // Output new language stats
                foreach( TextTable.SupportedLanguages language in languages ) {
                    List<String> missingIds = new();
                    foreach( string textId in allLanguages[TextTable.SupportedLanguages.eng].Keys ) {
                        if( !allLanguages[language].ContainsKey( textId ) ) {
                            missingIds.Add( textId );
                        }
                    }

                    if( missingIds.Count > 0 ) {
                        Debug.Log( $"Missing Ids for {language}: {String.Join( ", ", missingIds )}" );
                    }
                }

            } catch( System.Exception e ) {
                Debug.Log( "Error reading file: " + e.Message + " " + e.StackTrace );
            }

            setupLangugageToggles();
        }
    }

    IEnumerator saveTextTable() {

        FileBrowser.SetFilters( false, new FileBrowser.Filter( "json", ".json" ) );
        yield return FileBrowser.WaitForSaveDialog( FileBrowser.PickMode.Files, false, "C:\\dev\\MightierApp\\Assets\\Plugins\\NMPlatform\\Resources", "outputtexttable.json", "Save Text Table" );
        if( FileBrowser.Success ) {
            string path = FileBrowser.Result[0];
            try {
                                
                // Stream one line at a time to a file at path. Create if necessary.
                using( StreamWriter writer = new StreamWriter( path ) ) {
                    writer.WriteLine( "{ \"textTable\": [" );

                    int textIdCount = allLanguages[TextTable.SupportedLanguages.eng].Count;
                    for( int i = 0; i < textIdCount; i++ ) {
                        writer.WriteLine( "\t{" );
                        String textId = allLanguages[TextTable.SupportedLanguages.eng].Keys.ElementAt( i );
                        
                        writer.WriteLine( $"\t\t\"textId\": \"{textId}\"," );

                        int languageCount = 0;
                        foreach( TextTable.SupportedLanguages language in Enum.GetValues( typeof( TextTable.SupportedLanguages ) ) ) {
                            if( allLanguages.ContainsKey( language ) ) {
                                if( allLanguages[language].ContainsKey( textId ) ) {
                                    languageCount++;
                                }
                            }
                        }

                        foreach( TextTable.SupportedLanguages language in Enum.GetValues( typeof( TextTable.SupportedLanguages ) ) ) {
                            if( allLanguages.ContainsKey( language ) ) {
                                if( allLanguages[language].ContainsKey( textId ) ) {
                                    String ending = --languageCount > 0 ? "," : "";
                                    String escapedText = allLanguages[language][textId].Replace( "\r\n", "\n" ).Replace( "\n", "\\n" );                                    
                                    writer.WriteLine( $"\t\t\"{language}\": \"{escapedText}\"{ending}" );
                                }
                            }
                        }

                        String entryEnding = i < ( textIdCount - 1 ) ? "," : "";
                        writer.WriteLine( $"\t}}{entryEnding}" );
                    }

                    writer.WriteLine( "] }" );
                }
                
                Output.text += $"\nSaved to {path}";
                
                
            } catch( System.Exception e ) {
                Debug.Log( "Error writing file: " + e.Message + " " + e.StackTrace );
            }
        }
    }
}
