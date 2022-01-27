# Vodeo ECS Framework for Unity
Full documentation here: https://github.com/VodeoGames/VodeoECS/wiki

## What is this for?

The Vodeo ECS framework is an "Entity Component System" framework for game development with the Unity game engine. 

ECS architecture is specialized for high-performance real-time processing of large quantities of dynamic entities. It takes a modular, data-driven approach by defining all "Entities" as sets of simple and modular data-only "Components", which are then processed by various "Systems", each System having a specific and encapsulated function.

As of this writing Unity is developing its own official ECS framework as part of the "DOTS" project, which is likely to continue undergoing significant changes in the near future. The Vodeo ECS is a separate project which was created as a custom-built and more stable alternative to the Unity ECS, while still taking advantage of Burst compilation and the Jobs system.

## What are the main features of the Vodeo ECS framework?

* Supports Burst compilation and the DOTS job system, but can also be used without any knowledge of these features. 

* Moddability out of the box through runtime loading of .json files defining all basic Entity "prototypes", which are instantiated by the game.

* Saving and loading system supporting serialization and deserialization of the entire game state automatically including any user-defined data.

* A custom Unity editor view for inspecting and modifying Component data in play mode. 

* The Vodeo ECS architecture is based on a unique "best-of-both worlds" solution to the problem of choosing between the "Archetypal" and "Sparse sets" approaches to component storage.

## How to install the framework?

<img align="right" src="https://user-images.githubusercontent.com/65035652/151412713-75dde5d4-5b57-4e0b-80ff-9260747704a3.png">

1. Create a new Unity project

2. In the Package Manager click on the "+" button then "Add package from git URL" and copy-paste this URL: https://github.com/JoachimDL/VodeoECS/

3. If you'd like to check out the sample demonstration (recommended if you're new to the framework!) Open the "Samples" dropdown and import the sample. Then load up the imported scene in your Samples folder!


<img align="right" src="https://user-images.githubusercontent.com/65035652/151412845-8d271233-9428-4f19-95f3-fa1cd93e0b69.png">
