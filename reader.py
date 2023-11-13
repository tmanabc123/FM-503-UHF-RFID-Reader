import time

class Reader():
    def __init__(self, ser) -> None:
        """
        RFID reader object
        """
        self.ser = ser

    def clear_serial_buffers(self):
        """
        flush serial data buffers
        """
        self.ser.reset_input_buffer()
        self.ser.reset_output_buffer()


    def read(self) -> str:
        """
        Read from serial interface. 

        return: string hex representation of 16 bit words
        note - if 0xE280 is sent, add E2 and 80 end to end bitwise and that will
        represent the word in MSB first format
        """
        # wait for \n response
        while self.ser.readline() != b'\n':
                time.sleep(0.0001)
        # read response
        data = self.ser.readline()
        # decode from bytes to characters
        decoded = data.decode()
        # remove <CR><LF>
        return decoded.replace('\r\n', '')
    
    def hex_str_to_int_list(self, input_string:str, reversed:bool=False):
        """
        input_string: a string of hex words in MSB first format. EX: 'E2801160' for a two word string
        reversed: if True, output will be LSB first

        return: words (int) - MSB or LSB first depending on value of 'reversed'
        """
        if len(input_string) >=4:
            # split into hex strings
            hex_list = [input_string[i:i+4] for i in range(0, len(input_string), 4)]
            # convert to ints
            int_values = list(map(lambda x: (int(x[0:2], 16) << 8) | int(x[2:4], 16), hex_list))
            if reversed:
                return list(map(lambda x: int(bin(x)[2:].zfill(16)[::-1],2), int_values))
            else:
                return list(map(lambda x: int(bin(x)[2:].zfill(16),2), int_values))
        else:
            return False

    def hex_str_to_bin_list(self, input_string, reversed:bool=False):
        """
        input_string: a string of hex words in MSB first format. EX: 'E2801160' for a two word string
        reversed: if True, output will be LSB first

        return: words (int) - MSB or LSB first depending on value of 'reversed'
        """
        if len(input_string) >=4:
        # split into hex strings
            hex_list = [input_string[i:i+4] for i in range(0, len(input_string), 4)]
            # convert to ints
            int_values = list(map(lambda x: (int(x[0:2], 16) << 8) | int(x[2:4], 16), hex_list))
            if reversed:
                return list(map(lambda x: bin(x)[2:].zfill(16)[::-1], int_values))
            else:
                return list(map(lambda x: bin(x)[2:].zfill(16), int_values))
        else:
            return False

    def convert_to_raw(self, input_int_list):
        """
        converts MSB first int list to large raw binary blob 

        input_int_list: MSB first word list

        return: binary string padded to 96 bits long (LSB in element 0)
        """
        # convert to bin
        output_binary_string = ''
        for i in range(len(input_int_list)):
            output_binary_string += bin(input_int_list[i])[2:].zfill(16)
        return output_binary_string


    def read_TID_bank(self, addr:int=0, words:int=6, raw:bool=False):
        """
        Read lower 96 bytes from TID bank (Bank 2)

        (lower 48 and extended)

        addr: Starting address
        len: Words to read from tag
        raw: Returns the hex string if true

        return: LSB first converted ints (default)
                HEX string if raw is set to True
        """
        to_write = "\nR2,{},{}\r".format(addr,words).encode('utf-8')
        self.ser.write(to_write)
        # read and replace the "R" command readback byte
        string_response =  self.read().replace('R', '').encode('utf-8')
        # if no tag read, return False
        if len(string_response) <= 2:
            return False
        if raw:
            return string_response
        else:
            return self.hex_str_to_int_list(string_response)
        
    def read_EPC_bank(self, words:int=8, raw:bool=False, crc:bool=True):
        """
        Read single tag EPC bank 
        Returned data from the reader is CRC16+PC+EPC

        words: number of words to read from EPC bank

        raw: if True, the raw hex string from the reader will be returned

        return: LSB first converted ints (default)
                HEX string if raw is set to True
        """
        to_write = "\nR1,0,{}\r".format(words).encode('utf-8')
        self.ser.write(to_write)
        # read and replace the "R" command readback byte
        string_response_bytes =  self.read().replace('R', '').encode('utf-8')

        # if no tag read, return False
        if len(string_response_bytes) <= 2:
            return False
        
        # check CRC. if bad, return error
        if crc:
            # convert from ASCII bytes "string" to an actual python string
            string_form = str(string_response_bytes)
            # get the CRC value read from the tag in int form by converting the base 16 (hex) string to an int 
            crc_from_tag = int(string_form[2:6], 16)
            # separate the PC+EPC data from the full response
            pc_and_epc_string = string_form[6:-1]
            # convert the hex string to bytes
            input_bytes = bytes.fromhex(pc_and_epc_string)
            # calculate CRC
            crc_calculated = self.crc16(input_bytes)
            if crc_calculated == crc_from_tag:
                # print("CRC Good")
                if raw:
                    return string_response_bytes
                else:
                    return self.hex_str_to_int_list(string_response_bytes)
            else:
                # print("CRC Bad")
                return False
        else:
            if raw:
                return string_response_bytes
            else:
                return self.hex_str_to_int_list(string_response_bytes)
        
    def multi_tag_EPC_read(self, raw=False, crc=True, max=4):
        """
        Read EPC of multiple tags

        raw: if true, the output will be the raw PC+EPC+CRC16 data
        
        crc: if true, the crc will be checked before returning the EPC data split into words

        Note: For some reason, the reader outputs the data differently compared to single EPC read.
            Here, the output format is PC+EPC+CRC16
        """
        to_write = "\nU{}\r".format(max).encode('utf-8')
        self.ser.write(to_write)
        # until the end of list has detected, read lines
        data = []
        while True:
            # wait for \n response
            while self.ser.readline() != b'\n':
                    time.sleep(0.0001)
            # read the EPC tag data
            string_response_bytes = self.ser.readline()
            # if end of tag list detected, break
            if string_response_bytes == b'U\r\n':
                break
            # continue with data processing
            if crc:
                # convert from ASCII bytes "string" to an actual python string
                string_form = str(string_response_bytes)
                # get the CRC value read from the tag in int form by converting the base 16 (hex) string to an int 
                crc_from_tag = int(string_form[-9:-5], 16)
                # separate the PC+EPC data from the full response
                pc_and_epc_string = string_form[3:-9]
                # convert the hex string to bytes
                input_bytes = bytes.fromhex(pc_and_epc_string)
                # calculate CRC
                crc_calculated = self.crc16(input_bytes)
                # if CRC is good
                if crc_calculated == crc_from_tag:
                    print("CRC good")
                    # re-format data from PC+EPC+CRC16 to CRC16+PC+EPC
                    formatted = string_form[-9:-5]+string_form[3:-9] 
                    if raw:
                        data.append(formatted)
                    else:
                        data.append(self.hex_str_to_int_list(formatted))
                # if crc is bad
                else:
                    print("CRC bad")
            else:
                # if crc not checked, just use the data without checking
                # re-format data from PC+EPC+CRC16 to CRC16+PC+EPC
                formatted = string_form[-9:-5]+string_form[3:-9] 
                if raw:
                    data.append(formatted)
                else:
                    data.append(self.hex_str_to_int_list(formatted))
        return data


    def multi_tag_general_read(self, raw=True):
        """
        Read the EPC and other data of multiple tags using the "UR" command
        """
        return True

    def reader_ID(self):
        """
        Return the reader ID
        """
        self.ser.write(b'\nS\r')
        return self.read()
    
    def crc16(self, data: bytes) -> int:
        """
        Calculate ISO/IEC 13239 CRC

        Defined by:
        initial CRC: 0xFFFF
        reflect input: False
        polynomial: 0x1021 (X^16+X^12+X^5+1)
        reflect output: False
        XOR output: 0xFFFF
        """
        # Define the polynomial (0x1021) used in CRC calculation
        poly = 0x1021
        # Initialize the CRC value to 0xFFFF
        crc = 0xFFFF

        # Iterate over each byte in the input data
        for byte in data:
            # Combine the current byte with the CRC register (XOR operation)
            # Shift left by 8 bits to process the next byte
            crc ^= byte << 8
            # Process each bit in the byte
            for _ in range(8):
                # Check if the leftmost bit of the CRC is a 1
                if (crc & 0x8000):
                    # If set, shift CRC left by 1 and XOR with the polynomial
                    crc = (crc << 1) ^ poly
                else:
                    # If not set, shift the CRC left by 1
                    crc = crc << 1
                
                # mask with 0xFFFF to keep the crc value 16 bits
                crc &= 0xFFFF

        # XOR the final crc value with 0xFFFF
        return crc ^ 0xFFFF