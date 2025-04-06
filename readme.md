
# Charmeleon

**Charmeleon** is a user-friendly impedance visualization tool designed for use in EEG research with **TMSi Refa amplifiers**. It provides intuitive and clear feedback about electrode impedance quality, enabling researchers to quickly and confidently assess signal readiness before and during EEG acquisition. The application is highly configurable and supports different electrode montages out of the box.

## Features

- **Live impedance visualization** from TMSi Refa amplifiers.
- **Clear color-coded feedback** to indicate electrode contact quality.
- **Customizable montages** for flexible setup in different experimental configurations.
- **Intuitive GUI** suitable for lab technicians and researchers alike.
- **Modular design** for easy integration and adaptation.

## Screenshot

*Add a screenshot or animation here to show the main GUI.*

## Use Cases

- Pre-session impedance checks in EEG experiments.
- Flexible adaptation to research protocols involving different cap layouts or montages.

## Installation

### Requirements

- Windows 10 or later
- .NET Framework (required for C# GUI)
- TMSi SDK installed (you may need to request access from [TMSi](https://www.tmsi.com/))

### Dependencies

- Visual Studio (for development)
- C++ build tools (for the real-time device driver)
- TMSi Refa drivers and libraries

### Building

1. Clone the repository:
   ```bash
   git clone https://github.com/markspan/Charmeleon.git
   ```
2. Open the solution file `CharmeleonGUI.sln` in Visual Studio.
3. Restore any missing NuGet packages.
4. Build the solution (Make sure target architecture matches your system, e.g., x64).

### Running

Once built, run the `CharmeleonGUI.exe` from the output directory.

## Configuration

- **Montage files**: Define the layout of electrodes and their labels.

If you have a specific montage you'd like to use, you can create it manually using this program. 

## Troubleshooting

- **No data or missing electrodes?** Make sure the amplifier is connected and recognized by the TMSi software stack.
- **Impedance values not showing?** Confirm the montage file matches the connected cap layout.
- **Crashes on startup?** Try running the app as Administrator and ensure all required DLLs from the TMSi SDK are present in the executable directory. 

- Dark mode


## Contributing

Currently, this project is in active development. Contributions, feature requests, and bug reports are welcome! Please file an issue or fork the repository and submit a pull request.

## License

Free to use, falls under GNU-GPLv2

