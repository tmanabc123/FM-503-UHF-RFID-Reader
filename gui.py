from PyQt6.QtWidgets import *
from PyQt6.QtCore import *
from PyQt6 import QtGui
import sys
import glob
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


class Main(QWidget):
    def __init__(self):
        super().__init__()
        self.setWindowTitle('RFID Reader')
        self.setGeometry(100, 100, 950, 500)
        # create grid layout
        self.layout = QGridLayout()
        # set layout on window
        self.setLayout(self.layout)

        # Data table stuff
        self.table_headers = ["ISO/IEC 15963 Tag", "XTID", "S bit", "F bit", "MDID", "Model", "XTID header", "38-Bit SN", "Times Read"]
        self.xtid_labels = ["XTID Length", "Optional cmd Support", "block W/Erase Support", "User Data/Perma Lock Support",
                        "Lock Bit Support", "RFU Data", "TDS 2.0 Compliant XTID Header"]
        self.table_headers_with_xtid = self.table_headers + self.xtid_labels
        self.current_table_headers = self.table_headers_with_xtid
        
        # possible types are "B" for binary, "I" for int, and "D" for decoded
        self.table_display_type = "D"
        self.display_XTID_details = True

        
        # serial stuff
        # get list of devices (works for all platforms)
        self.available_serial_devices = list(map(lambda com_device: com_device.name, comports()))
        self.selected_device = None
        self.baudrate = 38400
        self.ser = None

        # tag reading stuff
        self.reader = None
        self.unique_ids = []
        self.tag_database = {}
        self.available_modes = ["TID","EPC","EPC-Multu"]

        # bool to keep track of whether or not a read function is in process
        # used to prevent overlap when spawning a new thread
        self.reading = False


        # init UI
        self.initUI()
        self.create_toolbar()

        # Initialize timer
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.update_loop)
        self.update_rate = 20



    def initUI(self):
        ######################################### First Row ############################
        # grid width
        width = 3
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
        label.setText("Select Serial Device: ")
        label.setAlignment(Qt.AlignmentFlag.AlignHCenter | Qt.AlignmentFlag.AlignVCenter)
        label.setFixedWidth(200)
        self.layout.addWidget(label, row,0)

        # ############## select device dropdown ##############
        self.device_select_box = CustomComboBox()
        self.device_select_box.addItems(self.available_serial_devices)
        self.device_select_box.activated.connect(self.update_selected_serial_device)
        self.device_select_box.clicked.connect(self.refresh_serial_devices)
        self.layout.addWidget(self.device_select_box, row, 1)

        # ############## button to start logging ##############
        self.start_logging_button = QPushButton(self, text='Start Logging')
        self.start_logging_button.setStyleSheet(green_button_style_shet)
        self.start_logging_button.clicked.connect(self.start_log)
        self.layout.addWidget(self.start_logging_button,row,2)


        ######################################### Second Row ############################
        row += 1
        # ############## label for open File ##############
        label = QLabel(self)
        label.setFont(QtGui.QFont('Arial', 15)) 
        label.setText("Select Input File: ")
        label.setAlignment(Qt.AlignmentFlag.AlignHCenter | Qt.AlignmentFlag.AlignVCenter)
        label.setFixedWidth(200)
        self.layout.addWidget(label, row,0)

        

        ######################################### Third Row ############################
        row += 1
        # ############## label for mode select ##############
        label = QLabel(self)
        label.setFont(QtGui.QFont('Arial', 15)) 
        label.setText("Select Read Mode: ")
        label.setAlignment(Qt.AlignmentFlag.AlignHCenter | Qt.AlignmentFlag.AlignVCenter)
        label.setFixedWidth(200)
        self.layout.addWidget(label, row,0)

        # mode select between TID, EPC single, EPC multi, and multi segment
        # ##############  Mode Selection ##############
        self.read_mode_box = CustomComboBox()
        self.read_mode_box.addItems(self.available_modes)
        self.read_mode_box.activated.connect(self.update_selected_mode)
        self.layout.addWidget(self.read_mode_box, row, 1)


        # open log file to read



        ######################################### Data Display block ############################
        # ############## section label ##############
        row += 1
        section_one_label = QLabel(self)
        section_one_label.setMaximumHeight(30)
        section_one_label.setText("Unique Tags")
        section_one_label.setFont(QtGui.QFont('Arial', 20))
        self.layout.addWidget(section_one_label, row,0)

        # ############## export log button ##############
        self.export_log_button = QPushButton(self, text='Export Log')
        self.export_log_button.setStyleSheet(blue_button_style_shet)
        self.export_log_button.clicked.connect(self.export_log)
        self.layout.addWidget(self.export_log_button,row,1)

        # ############## reset log button ##############
        self.reset_log_button = QPushButton(self, text='Clear Log')
        self.reset_log_button.setStyleSheet(red_button_style_shet)
        self.reset_log_button.clicked.connect(self.clear_log)
        self.layout.addWidget(self.reset_log_button,row,2)


        # ############## data table ##############
        row += 1
        self.data_table = QTableWidget()
        self.layout.addWidget(self.data_table, row, 0, 1, width)
        


        ######################################### END block ############################
        # Add vertical spacer at the end
        # row += 1
        # spacer = QSpacerItem(20, 40, QSizePolicy.Policy.Minimum, QSizePolicy.Policy.Expanding)
        # self.layout.addItem(spacer, row, 0, 1, width)
        # # Set row stretch for the last row
        # self.layout.setRowStretch(1, 1)



    def create_toolbar(self):
        self.menubar = QMenuBar()
        self.layout.addWidget(self.menubar, 0, 0)
        self.file_menu = self.menubar.addMenu("File")
        self.file_menu.addAction("New")
        self.file_menu.addAction("Open")
        self.file_menu.addAction("Save")
        self.file_menu.addSeparator()
        self.file_submenu = self.file_menu.addMenu("Read Specific Tag")
        self.file_submenu.addAction("Monza R6")

    def start_log(self):
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


    def update_loop(self):
        """
        Read data from the reader every N seconds and then update the data
        """
        # collect new data
        tid_bank_data = self.reader.read_TID_bank()
        # break if no tag was read
        if tid_bank_data == False:
            print("No Tag Detected")
            return False
        
        bin_data = self.reader.convert_to_raw(tid_bank_data)
        # if data is new
        if bin_data in self.tag_database:
            print("Duplicate Tag Detected... Ignoring")
            print("TID Data: {}".format(bin_data))
            #increment tag read counter
            self.tag_database[bin_data] += 1
        else:
            print("New tag detected... Adding to database")
            print("TID Data: {}".format(bin_data))
            # add new tag to db and set count to 1
            self.tag_database[bin_data] = 1

        self.update_data_table()


    def stop_log(self):
        print("logging stopped")
        # stop timer
        self.timer.stop()
        # change button to start configuration
        self.start_logging_button.setText('Start Logging')
        self.start_logging_button.setStyleSheet(green_button_style_shet)
        # disconnect old connections
        self.start_logging_button.clicked.disconnect()
        self.start_logging_button.clicked.connect(self.start_log)
        self.start_logging_button.update()
        # closer serial interface
        self.ser.close()
        print("serial interface closed")

    def clear_log(self):
        """
        clear the log
        """
        self.tag_database = {}
        self.update_data_table()

    def export_log(self):
        """
        Save log to file
        """
        print("Saving Log to file")


    def update_data_table(self):
        """
        This function takes the tag database and updates the displayed table to reflect the
        tags in the database
        """
        # do nothing if there is nothing in database
        if len(self.tag_database) > 0:
            print("database length: {}".format(len(self.tag_database)))
            # Update table column count and headers
            self.data_table.setColumnCount(len(self.current_table_headers))
            self.data_table.setHorizontalHeaderLabels(self.current_table_headers)
            # update number of rows
            self.data_table.setRowCount(len(self.tag_database))
            self.data_table.resizeColumnsToContents()
            # for each element (tag) in self.tag_database, add that row and it's elements to the table as is (binary)
            for row_index, row in enumerate(self.tag_database):

                # ======= Display basic 48 bit TID header information ========
                # convert the raw binary in the tag database to partitioned binary strings
                segmented_TID_binary_data = segment_TID_data(True, row)
                # decode row to human readable
                interpreted_TID = interpret_lower_48_TID(segmented_TID_binary_data)
                if self.table_display_type == "D":
                    segmented_TID_binary_data = interpreted_TID
                for column_index, value in enumerate(segmented_TID_binary_data):
                    # updat table based on the correct display type
                    if self.table_display_type == "B":
                        self.data_table.setItem(row_index, column_index, QTableWidgetItem(value))
                    elif self.table_display_type == "I":
                        # convert to int (must be flipped since the string representation is LSB in elementn 0 and 
                        # we need to have LSB in highest element)
                        int_value = str(int(value[::-1],2))
                        self.data_table.setItem(row_index, column_index, QTableWidgetItem(int_value))
                    elif self.table_display_type == "D":
                        item = QTableWidgetItem(str(value))
                        if value == "False":
                            item.setForeground(QtGui.QBrush(QtGui.QColor(224, 61, 61))) # Red
                        if value == "True":
                            item.setForeground(QtGui.QBrush(QtGui.QColor(42, 173, 48))) # Green
                        self.data_table.setItem(row_index, column_index, item)

                # display serial number
                column_index += 1
                sn_bin = extract_serial_num(interpreted_TID, row)
                sn = "Unknown Tag"
                if type(sn_bin) is not type(None):
                    sn = str(int(sn_bin, 2))
                item = QTableWidgetItem(str(sn))
                item.setForeground(QtGui.QBrush(QtGui.QColor(66, 227, 245)))
                self.data_table.setItem(row_index, column_index, item)

                # display times read counter
                column_index += 1
                read_count = self.tag_database[row]
                item = QTableWidgetItem(str(read_count))
                self.data_table.setItem(row_index, column_index, item)

                # ===== add additional columns as needed ======
                if self.current_table_headers == self.table_headers_with_xtid:
                    column_index += 1
                    # get interpteted XTID data
                    interp_xtid_dat = interpret_XTID_header(segmented_TID_binary_data[6])
                    print("XTID segment data: {}".format(interp_xtid_dat))
                    for value in interp_xtid_dat:
                        item = QTableWidgetItem(str(value))
                        if value == "False":
                            item.setForeground(QtGui.QBrush(QtGui.QColor(224, 61, 61))) # Red
                        if value == "True":
                            item.setForeground(QtGui.QBrush(QtGui.QColor(42, 173, 48))) # Green
                        self.data_table.setItem(row_index, column_index, item)
                        column_index += 1

            # fit coumns to data
            self.data_table.resizeColumnsToContents()
        else:
            # display empty table
            self.data_table.setRowCount(0)


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

    def update_selected_mode(self):
        self.selected_mode = self.read_mode_box.currentText()
        print("Selected mode: {}".format(self.selected_mode))



# window that has analysis options
class multi_analysis_popup(QWidget):
    def __init__(self):
        self.a = True


    def initUI(self):
        # spectrum view button
        # mix at some frequency
        # display error bars
        return True


if __name__ == "__main__":
    app = QApplication([])
    app.setWindowIcon(QtGui.QIcon('deep_fried_burch.png'))
    mainWindow = Main()
    mainWindow.show()
    sys.exit(app.exec())
