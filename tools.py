# Author: Taylor Lindley
# Date: 10/28/2023
# Description: This module provids basic tools for analyzing the lower 48 bytes of 
#    a TID block. There are also other generic tag operations. Nothing specific to a tag brand
#    or model number should be included here.


import time
import json
from knownTags import *


# open json file containing mask designer ID and tag model number info
mdid_json_file = open('mdid_list.json')
# list of designers (element 0 of each one is the MDID)
mdid_data = json.load(mdid_json_file)['registeredMaskDesigners']

# open tag info json
TID_data = json.load(open('TID_info.json'))['associations']

def mdid_lookup(mdid:str) -> tuple:
    """
    look up MDID in json file

    mdid: string binary representation of mdid

    return: (manufacturer name, index)
    """
    json_index = None
    manufacturer_name = None
    for index, designer in enumerate(mdid_data):
        if designer['mdid'] == mdid:
            json_index = index
            manufacturer_name = designer['manufacturer']
    return manufacturer_name, json_index

def model_lookup(mdid_index:int, tmn:str) -> tuple:
    """
    look up tag model number given a manufacturer and the binary 
    string representation of it's model number

    mdid_index: index of the tag manufacturer
    tmn: bniary string representation of tag model number 

    return: (tag model name,  index)
    """
    chips = mdid_data[mdid_index]['chips']
    for index, chip in enumerate(chips):
        if chip['tmnBinary'] == tmn:
            return chip['modelName'], index
        
def segment_TID_data(binary_string_output:bool=False, input:str=False):
    """
    reads TID bank from tag and decodes in to memory segments

    binary_string_output: if set to true, the output will be in the form of a binary string
    input: binary string that the function will then segment into the lower 48 TID memory segments

    return: array memory segments (strings or int baed on input params - Default is int)
    """
    raw = input
    # if longger than 8 its, continue decoding
    if len(raw) > 8:
        # extract memory segments based on GS1s TDS 2.0 document
        # ISO / IEC 15963 Class Identifier 00h-07h
        class_identifier = raw[0:8]
        # XTID Indicator (X bit) 08h
        x = raw[8]
        # Security Indicator (S bit) 09h
        s = raw[9]
        # File Indicator (F bit) 0Ah
        f = raw[10]
        # Mask Designer Identifier (MDID) 0Bh-13h
        mdid = raw[11:20]
        # Tag Model Number (TMN) 14h-1Fh
        tmn = raw[20:32]
        # EPC Tag Data Standard Header 20h-2Fh
        epc_TD_standard_header = raw[32:48]

        decoded_segments_bin = [class_identifier, x, s, f, mdid, tmn, epc_TD_standard_header]
        print("48-Bit TID: {}".format(decoded_segments_bin))
        if binary_string_output:
            return decoded_segments_bin
        else:
            # convert each binary string element to an int
            decoded_segments_int = list(map(lambda x: int(x,2), decoded_segments_bin))
            return decoded_segments_int

def interpret_lower_48_TID(lower_48:list) -> str:
    """
    Interpret the lower 48 bits of the TID data

    lower_48: segmented array with each segment (MSB in element 0) determined by the standard 48 bit TID Header
    """
    # Determine Standard
    standard = TID_data['standard'][lower_48[0]]
    # Determine if extended tag ID is supported
    x = TID_data['XTIDBit'][lower_48[1]]
    # is security bit set?
    s = TID_data['SecurityBit'][lower_48[2]]
    # is file open command supported?
    f = TID_data['FileOpenBit'][lower_48[3]]
    # Get mask designer ID from json file
    designer, mdid_index = mdid_lookup(lower_48[4])
    # get tag model name
    try:
        model_name = model_lookup(mdid_index, lower_48[5])[0]
    except:
        # if no model name exists in the database
        model_name = "Unknown: {}".format(lower_48[5])

    # EPC XTID header - do nothing for now
    binary_XTID = lower_48[6]

    return [standard, x, s, f, designer, model_name, binary_XTID]

def interpret_XTID_header(binary_header:str) -> list:
    """
    given a binary string representation of the XTID header,
    decode and return arrayas describit the analyzed header

    binary_header: XTID header in binary string format (MSB is in element 0)

    returns: user readable analysis of XTID header data
    """
    # calculate XTID serialization length (actual bits may mean different things for different manufacturers)
    serialization_bits = binary_header[0:3]
    # calculate xtid serial length
    xtid_ser_length = 48 + ((int(serialization_bits,2)-1)*16)
    # see if optional commands are supported
    optional_commands_supported = "True" if (binary_header[3] == '1') else "False"
    # check if block write and block erase segment is included
    block_we = "True" if (binary_header[4] == '1') else "False"
    # is user memory and block perma lock present?
    user_mem_and_lock = "True" if (binary_header[5] == '1') else "False"
    # lock bit support?
    lock_bit_support = "True" if (binary_header[6] == '1') else "False"
    # 1-8 reserved for future use
    rfu = binary_header[6:14]
    # extended header present? this should be zero if compliant with TDS 2.0
    extended_xtid = "False" if (binary_header[15] == '1') else "True"

    #put into list and return
    return [xtid_ser_length, optional_commands_supported, block_we, user_mem_and_lock, lock_bit_support, rfu, extended_xtid]

def extract_serial_num(interpreted_TID_data:list, raw_bin_string:str) -> int:
    """
    given the manufacturere data (from interpreted_TID_data) extract the tags serial number
    from the raw binary string

    return: serial number
    """
    if interpreted_TID_data[4] == "Impinj":
        if interpreted_TID_data[5] == "Monza R6":
            # extract Monza R6 38-bit serial number
            return impinj_mr6.extract_38_Bit_serial_number(raw_bin_string)

    else:
        return None


green_button_style_shet = """QPushButton{
    background-color: #289c47;
    border-radius:4px;
    padding:4px;
}

QPushButton:hover{
    background-color: #58ae6f;
}"""

red_button_style_shet = """QPushButton{
    background-color: #9d2828;
    border-radius:4px;
    padding:4px;
}

QPushButton:hover{
    background-color: #a44a4a;
}"""