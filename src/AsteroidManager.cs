/** Determines which asteroid gets which orbit
 * @file Population.cs
 * @author Starstrider42
 * @date Created April 10, 2014
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Is there a good way to sync version number between here, doxygen.cfg, the markdown source, and Git tags?
[assembly:AssemblyVersion("0.2.0")]

namespace Starstrider42 {

	namespace CustomAsteroids {
		/** Central class for controlling asteroid orbits
		 */
		internal static class AsteroidManager {
			static AsteroidManager() {
				try {
					allowedPops = PopulationLoader.Load();
					totalRate = allowedPops.getTotalRate();
				} catch (Exception) {
					// Ensure the contents of AsteroidManager are predictable even in the event of an exception
					// Though an exception thrown by a static constructor is basically unrecoverable...
					allowedPops = null;
					totalRate = 0.0;
					throw;
				}
			}

			private static PopulationLoader allowedPops;
			private static double totalRate;

			/** Customizes an asteroid, based on the settings loaded to Custom asteroids
			 * 
			 * @param[in,out] asteroid The asteroid to be modified
			 * 
			 * @pre @p asteroid is an asteroid object in-game
			 * 
			 * @post @p asteroid has properties consistent with membership in a randomly 
			 * 		chosen population
			 * 
			 * @exception System.InvalidOperationException Thrown if there are no populations in 
			 * 		which to place the asteroid
			 * @exception Starstrider42.CustomAsteroids.AsteroidManager.BadPopulationException Thrown if a 
			 * 		population exists, but cannot generate valid data
			 * 
			 * @exceptsafe The program is in a consistent state in the event of an exception
			 */
			internal static void editAsteroid(Vessel asteroid) {
				Population newPop = allowedPops.drawPopulation();

				try {
					asteroid.orbitDriver.orbit = newPop.drawOrbit();
				} catch (InvalidOperationException e) {
					throw new BadPopulationException (newPop, 
						"CustomAsteroids: Selected invalid population " + newPop, e);
				}

			}

			internal class BadPopulationException : System.InvalidOperationException {
				public BadPopulationException() : base() {
					badPop = null;
				}

				public BadPopulationException(Population which) : base() {
					badPop = which;
				}

				public BadPopulationException(Population which, string message) : base(message) {
					badPop = which;
				}

				public BadPopulationException(Population which, string message, Exception inner) 
					: base(message, inner) {
					badPop = which;
				}

				protected BadPopulationException(System.Runtime.Serialization.SerializationInfo info, 
						System.Runtime.Serialization.StreamingContext context)
					: base(info, context) {}

				public Population getPop() {
					return badPop;
				}

				private Population badPop;
			}
		}

		/** Stores raw asteroid data
		 * 
		 * @invariant At most one instance of this class exists
		 * 
		 * @todo Clean up this class
		 */
		internal class PopulationLoader {
			/** Creates an uninitialized solar system
			 * 
			 * @post No asteroids will be created
			 * 
			 * @exceptsafe Does not throw exceptions.
			 */
			internal PopulationLoader() {
				asteroidSets = new List<Population>();
			}

			/** Factory method obtaining Custom Asteroids settings from a config file
			 * 
			 * @return A newly constructed PopulationLoader object containing up-to-date 
			 * 		settings from the Custom Asteroids config file, or the default settings 
			 * 		if no such file exists.
			 * 
			 * @exception Throws System.TypeInitializationException if the PopulationLoader object 
			 * 		could not be constructed
			 * 
			 * @exceptsafe The program is in a consistent state in the event of an exception
			 * 
			 * @todo Can I make Load() atomic?
			 * 
			 * @todo How to allow backward compatibility if the config file changes?
			 */
			internal static PopulationLoader Load() {
				Debug.Log("CustomAsteroids: loading settings...");
				try {
					// UrlConfig x;
					// x.parent.fullPath;		// Name of file to write to
					// x.config					// AsteroidSet node

					// Start with an empty population list
					PopulationLoader allPops = new PopulationLoader();

					// Search for populations in all config files
					UrlDir.UrlConfig[] configList = GameDatabase.Instance.GetConfigs("AsteroidSets");
					foreach (UrlDir.UrlConfig curSet in configList) {
						foreach (ConfigNode curNode in curSet.config.nodes) {
							if (curNode.name == "ASTEROIDGROUP") {
								#if DEBUG
								Debug.Log("Customasteroids: ConfigNode '" + curNode + "' loaded");
								#endif
								Population newPop = new Population();
								ConfigNode.LoadObjectFromConfig(newPop, curNode);
								allPops.asteroidSets.Add(newPop);
							}
							// ignore any other nodes present
						}
					}

					#if DEBUG
					foreach (Population x in allPops.asteroidSets) {
						Debug.Log("Customasteroids: Population '" + x + "' loaded");
					}
					#endif

					Debug.Log("CustomAsteroids: settings loaded");

					return allPops;
				// No idea what kinds of exceptions are thrown by ConfigNode
				} catch (Exception e) {
					throw new TypeInitializationException("Starstrider42.CustomAsteroids.PopulationLoader", e);
				}
			}

			/** Randomly selects an asteroid population
			 * 
			 * The selection is weighted by the spawn rate of each population; a population with 
			 * 		a rate of 2.0 is twice as likely to be chosen as one with a rate of 1.0.
			 * 
			 * @return A reference to the selected population
			 * 
			 * @exception System.InvalidOperationException Thrown if there are no populations from 
			 * 		which to choose one, or if all spawn rates are zero, or if any rate is negative
			 * 
			 * @exceptsafe Does not throw exceptions
			 */
			internal Population drawPopulation() {
				try {
					// A typedef! A typedef! My kerbdom for a typedef!
					List<Pair<Population, double>> bins = new List<Pair<Population, double>>();
					foreach (Population x in asteroidSets) {
						bins.Add(new Pair<Population, double>(x, x.getSpawnRate()));
					}

					return RandomDist.weightedSample(bins);
				} catch (ArgumentException e) {
					throw new InvalidOperationException("CustomAsteroids: could not draw population", e);
				}
			}

			/** Returns the total spawn rate of all asteroid populations. Currently only needed 
			 * 		for normalizing the asteroid selection.
			 * 
			 * @return The sum of all spawn rates for all populations.
			 * 
			 * @exceptsafe Does not throw exceptions
			 */
			internal double getTotalRate() {
				double total = 0.0;
				foreach (Population x in asteroidSets) {
					total += x.getSpawnRate();
				}
				return total;
			}

			/** Identifies the Custom Asteroids config file
			 * 
			 * @return An absolute path to the config file
			 * 
			 * @exceptsafe Does not throw exceptions
			 */
			private static string optionList() {
				return KSPUtil.ApplicationRootPath + "GameData/Starstrider42/CustomAsteroids/Custom Asteroids Settings.cfg";
			}

			/////////////////////////////////////////////////////////
			// Config options
			// Giving variables upper-case names because it looks better in the .cfg file

			//[Persistent(name="AsteroidSets",collectionIndex="ASTEROIDGROUP")]
			private List<Population> asteroidSets;
		}
	}
}