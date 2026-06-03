# TotallyNotRushHour
A 6x6 grid-based sliding puzzle game inspired by Rush Hour, where players free a target car by moving blocking vehicles without rotating or colliding them.

## Objective
Guide the red target car to the exit by sliding other vehicles out of the way

## Current Basic Functionality
- Difficulty level select 
- Procedural board generation for a 6x6 puzzle grid 
- Car spawning from puzzle data
- Car selection by mouse click
- Movement constrained by orientation:
	- Horizontal cars move left/right only
	- Vertical cars move up/down only
- Collision and boundary checks block invalid moves
- Exit detection and win condition for the target car
- Puzzle reset that restores all cars back to initial positions
- Solution card *overlay*
- Audio system:
	- Background music loop
	- Car select, valid move, invalid move/collision, reset, and win SFX
	- UI button click SFX

## Controls
- Left Mouse Click: Select a car (available moves are highlighted)
	- Hold click and drag movement also available
- Arrow Keys: Move selected car (if move is valid)
- R: Reset puzzle
- Reset UI Button: Reset puzzle via UI
- Tab: Toggle Solution Card Overlay
- F1: toggles controls/rules 
- F2: toggles settings 
