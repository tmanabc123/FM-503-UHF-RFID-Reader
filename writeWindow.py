from PyQt6.QtWidgets import *
from PyQt6.QtCore import *
from PyQt6 import QtGui
import sys
from sys import platform

import serial
from serial.tools.list_ports import comports
import time
from tools import *
from reader import *

class CustomComboBox(QComboBox):
    # Define a custom signal
    clicked = pyqtSignal()

    def showPopup(self):
        # Emit the custom signal before showing the dropdown
        self.clicked.emit()
        super().showPopup()

class writeWindow(QWidget):
    def __init__(self):
        super().__init__()
        self.debug = True
        self.setWindowTitle('RFID Tag Writer')
        self.setGeometry(100, 100, 300, 500)
        # create grid layout
        self.layout = QGridLayout()

        # set layout on window
        self.setLayout(self.layout)

        # Transmit power level stuff
        self.available_power_levels = []
        for i in range(0, 0x1C):
            self.available_power_levels.append("{}dB".format(i-2))
        self.selected_tx_power_level = -2
        self.pwr_lvl_change = True
        
        # possible types are "B" for binary, "I" for int, and "D" for decoded
        self.table_display_type = "D"
        self.display_XTID_details = True
        
        # serial stuff
        # get list of devices (works for all platforms)
        self.available_serial_devices = list(map(lambda com_device: com_device.name, comports()))
        self.selected_device = None
        self.baudrate = 38400
        self.ser = None

        # init UI
        self.initUI()



    def initUI(self):
        ######################################### First Row ############################
        # grid width
        width = 1
        # adjust first row offset to account for menu bar (different for different OS)
        if platform == "win32":
            # windows
            row = 1
        elif platform == "darwin":
            # osx
            row = 0
        elif platform == "linux":
            row = 1

        # ############## label for select serial device ##############
        label = QLabel(self)
        label.setFont(QtGui.QFont('Arial', 15)) 
        label.setText("Select Serial Device:")
        label.setAlignment(Qt.AlignmentFlag.AlignRight | Qt.AlignmentFlag.AlignVCenter)
        label.setMinimumWidth(150)
        self.layout.addWidget(label, row,0)

        # ############## select device dropdown ##############
        self.device_select_box = CustomComboBox()
        self.device_select_box.addItems(self.available_serial_devices)
        self.device_select_box.activated.connect(self.update_selected_serial_device)
        self.device_select_box.clicked.connect(self.refresh_serial_devices)
        self.device_select_box.setMinimumWidth(220)
        self.layout.addWidget(self.device_select_box, row, 1)




        ######################################### Second Row ############################
        row += 1
        # ####### Label for transmit power #########
        label = QLabel(self)
        label.setFont(QtGui.QFont('Arial', 15)) 
        label.setText("Select Source:")
        label.setAlignment(Qt.AlignmentFlag.AlignRight | Qt.AlignmentFlag.AlignVCenter)
        label.setMinimumWidth(150)
        self.layout.addWidget(label, row,0)
        # ############## label for output power select ##############
        label = QLabel(self)
        label.setFont(QtGui.QFont('Arial', 15)) 
        label.setText("TX Power:")
        label.setAlignment(Qt.AlignmentFlag.AlignRight | Qt.AlignmentFlag.AlignVCenter)
        label.setMinimumWidth(150)
        self.layout.addWidget(label, row,1)

        # # mode select between TID, EPC single, EPC multi, and multi segment
        # # ##############  Mode Selection ##############
        # self.tx_power_box = CustomComboBox()
        # self.tx_power_box.addItems(self.available_power_levels)
        # self.tx_power_box.activated.connect(self.update_tx_power_level)
        # self.tx_power_box.setMinimumWidth(220)
        # self.layout.addWidget(self.tx_power_box, row, 1)

        # # ############## label for mode select ##############
        # label = QLabel(self)
        # label.setFont(QtGui.QFont('Arial', 15)) 
        # label.setText("Read Rate (ms):")
        # label.setAlignment(Qt.AlignmentFlag.AlignRight | Qt.AlignmentFlag.AlignVCenter)
        # label.setMinimumWidth(150)
        # self.layout.addWidget(label, row,2)

        # # mode select between TID, EPC single, EPC multi, and multi segment
        # # ##############  Mode Selection ##############
        # self.update_rate_box = CustomComboBox()
        # self.update_rate_box.addItems(self.available_update_rates)
        # self.update_rate_box.activated.connect(self.update_read_rate)
        # self.update_rate_box.setMinimumWidth(150)
        # self.layout.addWidget(self.update_rate_box, row, 3)


        # ######################################### Third Row ############################
        # # ############## section label ##############
        # row += 1
        # section_one_label = QLabel(self)
        # section_one_label.setMaximumHeight(30)
        # section_one_label.setText("Unique Tags")
        # section_one_label.setFont(QtGui.QFont('Arial', 20))
        # self.layout.addWidget(section_one_label, row,0)

        # # ############## export log button ##############
        # self.export_log_button = QPushButton(self, text='Export Log')
        # self.export_log_button.setStyleSheet(blue_button_style_shet)
        # self.export_log_button.clicked.connect(self.export_log)
        # self.layout.addWidget(self.export_log_button,row,1)

        # # ############## reset log button ##############
        # self.reset_log_button = QPushButton(self, text='Clear Log')
        # self.reset_log_button.setStyleSheet(red_button_style_shet)
        # self.reset_log_button.clicked.connect(self.clear_log)
        # self.layout.addWidget(self.reset_log_button,row,2)



        # # ############## button to start logging ##############
        # self.start_logging_button = QPushButton(self, text='Start Logging')
        # self.start_logging_button.setStyleSheet(green_button_style_shet)
        # self.start_logging_button.clicked.connect(self.start_log)
        # self.layout.addWidget(self.start_logging_button,row,3)


        # # ############## data table ##############
        # row += 1
        # self.data_table = QTableWidget()
        # self.layout.addWidget(self.data_table, row, 0, 1, width)


        # ######################################### END block ############################
        # # Add vertical spacer at the end
        # # row += 1
        # # spacer = QSpacerItem(20, 40, QSizePolicy.Policy.Minimum, QSizePolicy.Policy.Expanding)
        # # self.layout.addItem(spacer, row, 0, 1, width)
        # # # Set row stretch for the last row
        # # self.layout.setRowStretch(1, 1)



    def write_data(self):
        print("logging started")
        # start logging
        try:
            if platform == "darwin":
                self.ser = serial.Serial("/dev/"+self.selected_device, self.baudrate, timeout=1)
            elif platform == "linux":
                self.ser = serial.Serial("/dev/"+self.selected_device, self.baudrate, timeout=1)
            else:
                self.ser = serial.Serial(self.selected_device, self.baudrate, timeout=1)
                
            print("Serial interface opened")
            print("device: {}".format(self.selected_device))
            print("clearing input buffer")
            self.ser.reset_input_buffer()
            print("clearing output buffer")
            self.ser.reset_output_buffer()
        except Exception as error:
            print("Failed to open {}".format(self.selected_device))
            print("error message: {}".format(error))
            return False
        
        # change button to stop configuration
        self.start_logging_button.setText('Stop Logging')
        self.start_logging_button.setStyleSheet(red_button_style_shet)
        # disconnect old connections
        self.start_logging_button.clicked.disconnect()
        self.start_logging_button.clicked.connect(self.stop_log)
        self.start_logging_button.update()

        # instantiate reader object
        self.reader = Reader(self.ser)

        # start data collection
        # Start timer to call update_label every Nms
        self.timer.start(self.update_rate)


    def refresh_serial_devices(self):
        # refresh the available serial devices
        self.available_serial_devices = list(map(lambda com_device: com_device.name, comports()))
        # update dropdown options
        self.device_select_box.clear()
        self.device_select_box.addItems(self.available_serial_devices)
        print("updated device list")

    def update_selected_serial_device(self):
        # log selected device
        self.selected_device = self.device_select_box.currentText()
        print("selected device: {}".format(self.selected_device))

    def update_tx_power_level(self):
        # update transmit power levels 
        self.selected_tx_power_level = int(self.tx_power_box.currentText().replace("dB", ""))
        print("Changing TX power level to {}dB".format(self.selected_tx_power_level))
        self.pwr_lvl_change = True