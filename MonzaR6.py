
class MonzaR6():
    def __init__(self) -> None:
        pass
    
    def interpret_TID(self, binary_TID_data):
        """
        decode the upper 48 bytes of TID data

        input: binary string consisting of all 96 bits of TID data
        """
        raw = binary_TID_data
        # if longger than 8 its, continue decoding
        if len(raw) > 8:
            # extract memory segments based on impinj doc: 
            # TID MEMORY MAPS FOR MONZA SELF-SERIALIZATION - TECHNICAL REFERENCE
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
            # Wafer Mask Revision 30h-32h
            wafer_mask = raw[48:51]
            # Integra™ TID Parity 33h
            parity = raw[51]
            # Monza Series Cycle Counter 34h
            cycle_counter = raw[52]
            # Reserved for Future Use 50h-52
            reserved = raw[80:83]
            # Monza Series ID 53h-54h
            series_id = raw[83:85]
            # 38 bit Serial Number [55h:5Fh][40h:4Fh][35h:3Fh]
            serial_num_38 = raw[85:96] + raw[64:80] + raw[53:64]
            # 96 bit Serial Number [00h:07h][08h:13h][14h:1Fh][30h:32h]0 [50h:52h]0 0000 0000 0000 0[53h:54h]0 0[34h][55h:5Fh][40h:4Fh][35h:3Fh]
            serial_num_96 = raw[0:32] + raw[48:51] + '0' + raw[80:83] + '00000000000000' + raw[83:85] + raw[64:80] + serial_num_38

            decoded_segments_bin = [class_identifier, x, s, f, mdid, tmn, epc_TD_standard_header, wafer_mask, parity, cycle_counter, series_id, 
                                    reserved, serial_num_38, serial_num_96]
            return decoded_segments_bin

    def extract_38_Bit_serial_number(self, binary_TID_data) -> int:
        """
        input: binary string consisting of all 96 bits of TID data
        """
        return self.interpret_TID(binary_TID_data)[12]

    def extract_96_Bit_serial_number(self, binary_TID_data) -> int:
        """
        input: binary string consisting of all 96 bits of TID data
        """
        return self.interpret_TID(binary_TID_data)[13]