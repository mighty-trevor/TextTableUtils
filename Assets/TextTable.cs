using UnityEngine;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;

namespace NM {
	public static partial class TextTable {
		// Mapping from string IDs to strings for specific languages
		public static Dictionary<String, String> TextTableData;
		public static List<String> TextTableMissingIds = new List<String>();

		// The language will get set at application start.
		// IDs for languages must be from: http://www.loc.gov/standards/iso639-2/php/code_list.php
		public enum SupportedLanguages {
			uninitialized = 0,
			debug = 1,
			eng = 2,
			jpn = 3,
			zhs = 4,
			zht = 5,
			spa_mx = 6,

			/// latin american spanish spa-mx -- https://docs.unrealengine.com/4.26/en-US/ProductionPipelines/Localization/Overview/
			ara = 7,
			rus = 8,
			ukr = 9, // Ukrainian
		}

		public static SupportedLanguages currentLanguage = SupportedLanguages.uninitialized;

		public static string normalizeTextId( string text ) {
			// Remove all punctuation and spaces
			return Regex.Replace( text.ToUpper(), @"[^\w]", "" );
		}

		public static int GetTextCount() {
			return TextTableData.Count;
		}
		
		public static string[] GetAllTextIds() {
			string[] ids = new string[TextTableData.Keys.Count];
			TextTableData.Keys.CopyTo( ids, 0 );
			return ids;
		}

		public static bool DoesTextIdExist( string textId ) {
			if( TextTableData.ContainsKey( textId ) ) {
				return true;
			}

			return false;
		}

		// This also loads the text table. Taken from game.
		public static bool setCurrentLanguage( SupportedLanguages newLanguage ) {
			// If a requested language is not there, it should default to English.
			if ((newLanguage != currentLanguage) || true) {
				// Always do this. We set the language to the same language to reload the table.
				// First check that there's no mismatch between the list
				// of supported languages and what's actually in the file with the 
				// table containing all the text (filename is hardcoded for now).

				// If mismatch
				// Debug.Log("Language to " + newLanguage + " not available in table " + filename);

				// Now load table from file
				if ( TextTableUtilManager.RawTextTable != null ) {
					string textTableSerialized = TextTableUtilManager.RawTextTable;
					// Load and parse the JSON
					JSONObject textTableJsonReader = new JSONObject(textTableSerialized);

					// Set to null first in case we had a previously loaded table
					TextTableData = null;
					TextTableData = new Dictionary<String, String>();
					TextTableMissingIds = null;
					TextTableMissingIds = new();
					try {
						List<JSONObject> textElements = textTableJsonReader["textTable"].list;

						int firstExtraIndex = textElements.Count;

						for (int i = 0; i < textElements.Count; i++) {
							// Example of single textElement: 
							//  { 'textId': 'HITHERE', 'eng': 'Hey there!', 'fre': 'Salut mon ami!' }
							JSONObject thisTextElement = textElements[i];
							/*
							foreach (string key in thisTextElement.Keys) {
								Debug.Log("thisTextElement, " + key + ", " + thisTextElement[key]);
							}
							foreach (string key in _textTable.Keys) {
								Debug.Log("_textTable, " + key + ", " + _textTable[key]);
							}
							*/

							if (!thisTextElement.keys.Contains("textId") ||
							    thisTextElement["textId"] == null)
								// Possible formatting error. Not a fatal error, so just log it and skip this element.
								Debug.LogError("Check input file. Error reading element #" + i + ". Missing textId. Skipping this line.\n");
							else {
								string normalizedTextId = normalizeTextId(thisTextElement["textId"].str);
								if ((i < firstExtraIndex) &&
								    TextTableData.ContainsKey(normalizedTextId) &&
								    TextTableData[normalizedTextId] != null)
									// Same as above. Possible formatting error.
									Debug.LogError("Check input file. Error reading element #" + i + ". Already have an entry for " + thisTextElement["textId"] + ". Skipping this line.\n");
								else if (!thisTextElement.keys.Contains("eng") ||
								         thisTextElement["eng"] == null)
									// Same as above. Possible formatting error.
									Debug.LogError("Check input file. Error reading element #" + i + ", id '" + thisTextElement["textId"] + "'. Missing eng text. Skipping this line.\n");
								else if (!thisTextElement.keys.Contains(newLanguage.ToString()) ||
								         thisTextElement[newLanguage.ToString()] == null) {
									// This ID is not defined for this language.
									TextTableMissingIds.Add(thisTextElement["textId"].str);
								} else {
									// Text ID is valid and we have a translation for the desired language
									//Debug.Log( "Adding entry for " + normalizedTextId + ", " + thisTextElement[newLanguage.ToString()].str );
									TextTableData[normalizedTextId] = thisTextElement[newLanguage.ToString()].str.Replace("\\n", "\n"); // For some reason TMP doesn't do this
								}
							}
						}

					}
					catch (Exception e) {
						Debug.LogError("Check input file. Error reading serialized text table from file.\nError is " + e.ToString());
						return false;
					}
				} else {
					Debug.Log("Can't change language to " + newLanguage + " because it's already the current language.\n");
					return false;
				}

				// All done. Now set currentLanguage to newLanguage.
				TextTable.currentLanguage = newLanguage;
			} else {
				Debug.Log("Can't change language to " + newLanguage + " because it's already the current language.");
			}

			Debug.Log( "Opened text table for " + newLanguage + ".\n" );  
			return true;

		}		

		// Language must be set before lookupText can be called.
		
		
		public static String lookupText( String inputString ) {
			if( TextTable.currentLanguage == SupportedLanguages.uninitialized ) {
				Debug.LogError( "Language must be set before text can be looked up!\nCheck that loading data for this language succeeded." );
				return inputString;
			}

			string textId = normalizeTextId( inputString );
			string text = getTextFromTable( textId );

#if NM_DEV
            if(currentLanguage == SupportedLanguages.debug) {
                return "<b><[" + textId +"]></b>";
            }
#endif
			return text;
		}

		/// <summary>
		/// Lookup and format text. For cases where you want to call String.Format after the lookup.
		/// String.Format(lookup(inputString), args);
		/// </summary>
		public static String lookupText( String inputString, params object[] formatArgs ) {
			return String.Format( lookupText( inputString ), formatArgs );
		}

		private static string getTextFromTable( string ID ) {
			if( TextTableData.ContainsKey( ID ) ) {
				// This shouldn't be possible, but check anyway
				if( TextTableData[ID] == null )
					return string.Empty;
				else {
					return TextTableData[ID];
				}
			} else {
				Debug.LogError( "Didn't find text in table.\ntextId = " + ID + ", language = " + TextTable.currentLanguage.ToString() );
				return string.Empty;
			}
		}
	}

#if false // todo	
	public class SavedLanguageModel
	{
		public TextTable.SupportedLanguages Language;
		
		public JSONObject ToJSON()
		{
			JSONObject jsonObject = new JSONObject();

			jsonObject.AddField("Language", Language.ToString());
			
			return jsonObject;
		}

		public void FromJSON(JSONObject json)
		{
			string languageId = json.GetFieldOrDefault("Language", TextTable.SupportedLanguages.eng.ToString());
            Language = (TextTable.SupportedLanguages) Enum.Parse(typeof(TextTable.SupportedLanguages), languageId); 
		}
	}
#endif
	
	
}
