# OEM Bluetooth WP Raman Example UWP

This repository is meant as an example Bluetooth implementation of our OEM spectrometers. This example provides the backbone setup for a Bluetooth communication scheme in UWP and stubs our four example spectrometer commands : Ping, Firmware Revision, FPGA Revision, and a Spectrum Acquisition. The command structure and all available commands can be found in the API documentation linked below. 

To connect to the device you must have an OEM WP Raman spectrometer paired with your computer. Then select the device from the available bluetooth devices in the comboBox and click on the connect button. 

Specific stages and information are printed to the output terminal on the right.

If a specific command or method is unclear, please create an issue on the Issue tab of this repository.

![interface](images/interface.png)

## API Documentation
[A detailed API specification can be found on our WasatchDevice.com website. Just click this link to download a PDF version.](http://wasatchdevices.com/wp-content/uploads/2016/08/OEM-API-Specification.pdf)

If any part of this specification is unclear, do not hesitate creating an issue in this repository or emailing us directly.