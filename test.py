# Author: Taylor Lindley (t2@auburn.edu)
# Date: 10/19/2023
import serial
import time
from tools import *
from reader import *

baudrate = 38400
ser = serial.Serial('/dev/cu.usbserial-A50285BI', baudrate, timeout=1)

r = Reader(ser)

def reset_serial():
    # flush existing serial data
    ser.reset_input_buffer()
    ser.reset_output_buffer()

def version():
    """
    Return the reader version number
    """
    ser.write(b'\nV\r')
    # clear initial \n response
    ser.readline()
    # read actual response
    output = ser.readline()
    return output

def ID():
    """
    Return the reader ID
    """
    ser.write(b'\nS\r')
    # clear initial \n response
    ser.readline()
    # read actual response
    output = ser.readline()
    return output

def read():
    ser.write(b'\nR2,0,6\r')
    # clear initial \n response
    ser.readline()
    # read actual response
    output = ser.readline()
    return output

def read_loop():
    while True:
        ser.write(b'\nR2,0,6\r')
        # wait until <LF> from reader so we don't overwhelm it with writes
        while ser.readline() != b'\n':
            time.sleep(0.001)
        # read actual response
        output = ser.readline()
        print(output)

def read_range():
    ser.write(b'\nN4,00\r')
    # clear initial \n response
    ser.readline()
    # read actual response
    output = ser.readline()
    return output

def read_power_level():
    ser.write(b'\nN0,00\r')
    # clear initial \n response
    ser.readline()
    # read actual response
    output = ser.readline()
    return output

def set_power_level(pl):
    to_send = "\nN1,{}\r".format(pl)
    ser.write(to_send.encode('utf-8'))
    # clear initial \n response
    ser.readline()
    # read actual response
    output = ser.readline()
    return output