# TotallyNotRushHour

TotallyNotRushHour is a Unity sliding-block puzzle game inspired by Rush Hour. Players move cars around a 6x6 traffic grid to clear a path for the red target car and drive it through the exit.

## Objective

Free the red target car by sliding blocking vehicles out of the way. Cars cannot rotate, overlap, or move outside their allowed path.

## Features

- 6x6 Rush Hour-style puzzle board
- Difficulty-based level selection
- Curated puzzle data loaded from solved board records
- Endless mode with escalating puzzle difficulty
- Scoreboard and rank system based on solve time and grouped move efficiency
- Runtime board setup through the puzzle controller
- Car selection by mouse click
- Drag and keyboard movement controls
- Movement constrained by vehicle orientation:
  - Horizontal cars move left and right
  - Vertical cars move up and down
- Collision and boundary validation for invalid moves
- Highlighting for available moves
- Exit detection and win condition
- Puzzle reset to restore the starting layout
- Solution card overlay with board and move solution details
- Environment themes for normal difficulties
- Endless skybox with a day/night cycle
- Audio system with music, movement, collision, reset, win, and UI sounds

## Controls

- Left Mouse Click: Select a car
- Click and Drag: Move the selected car along its valid axis
- Arrow Keys: Move the selected car one valid step
- R: Reset the current puzzle
- Reset Button: Reset from the UI
- Tab: Toggle the solution card overlay
- F1: Toggle rules and controls
- F2: Toggle settings

## Endless Mode

Endless mode pulls from solved board data and presents puzzles in increasing difficulty based on minimum grouped move count. Scoring compares the player's grouped moves and solve time against the puzzle's known minimum solution.

Ranks range from `F` through `SSS`.

## Project Notes

- Built in Unity.
- Main gameplay scripts live under `rushHour/Assets/Scripts`.
- Environment scripts live under `rushHour/Assets/Environment/Scripts`.
- Solved puzzle data is stored in `rushHour/Assets/Resources/all_levels_solved.json`.
